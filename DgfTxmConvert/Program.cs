using LibDgf;
using LibDgf.Aqualead.Image;
using LibDgf.Aqualead.Image.Conversion;
using LibDgf.Aqualead.Texture;
using LibDgf.Dat;
using LibDgf.Graphics;
using LibDgf.Txm;
using McMaster.Extensions.CommandLineUtils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Path = System.IO.Path;

namespace DgfTxmConvert
{
    class Program
    {
        const string ETC1TOOL_PATH = @"etc1tool.exe";

        static List<IAlImageConverter> imageConverters;

        static int Main(string[] args)
        {
            imageConverters = new List<IAlImageConverter>
            {
                new PkmConverter(),
                //new KtxConverter(),
                new PngConverter(),
            };

            var app = new CommandLineApplication
            {
                Name = Path.GetFileName(Environment.GetCommandLineArgs()[0]),
                FullName = "DDG Final TXM Converter"
            };

            app.Command("convert-dat", config =>
            {
                config.FullName = "Convert DAT to images";
                config.Description = "Converts each individual TXM in a DAT into PNGs.";

                var datPathArg = config.Argument("datPath", "Path of the DAT to extract").IsRequired();
                datPathArg.Accepts().ExistingFile();
                var outBaseArg = config.Argument("outBase", "Directory to write extracted files");
                outBaseArg.Accepts().LegalFilePath();
                config.HelpOption();

                config.OnExecute(() =>
                {
                    ConvertDat(datPathArg.Value, outBaseArg.Value);
                });
            });

            app.Command("convert-txm", config =>
            {
                config.FullName = "Convert TXM to image";
                config.Description = "Converts TXM to PNG.";

                var txmPathArg = config.Argument("txmPath", "Path of the TXM to convert").IsRequired();
                txmPathArg.Accepts().ExistingFile();
                var outPathArg = config.Argument("outPath", "Path of converted PNG");
                outPathArg.Accepts().LegalFilePath();
                config.HelpOption();

                config.OnExecute(() =>
                {
                    ConvertTxm(txmPathArg.Value, outPathArg.Value);
                });
            });

            app.Command("replace-dat", config =>
            {
                config.FullName = "Replace image in DAT";
                config.Description = "Converts list of images and replaces entries in DAT file";

                var srcDatPath = config.Argument("srcDatPath", "Path of the source DAT").IsRequired();
                srcDatPath.Accepts().ExistingFile();
                var listPath = config.Argument("listPath", "Path of the image replacement list").IsRequired();
                listPath.Accepts().ExistingFile();
                var destDatPath = config.Argument("destDatPath", "Path to output DAT to").IsRequired();
                listPath.Accepts().LegalFilePath();
                config.HelpOption();

                config.OnExecute(() =>
                {
                    ReplaceDatImages(srcDatPath.Value, destDatPath.Value, listPath.Value);
                });
            });

            app.VersionOptionFromAssemblyAttributes(System.Reflection.Assembly.GetExecutingAssembly());
            app.HelpOption();

            app.OnExecute(() =>
            {
                app.ShowHelp();
                return 1;
            });

            try
            {
                return app.Execute(args);
            }
            catch (CommandParsingException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error while processing: {0}", ex);
                return -1;
            }
        }

        static void ReplaceDatImages(string srcDatPath, string destDatPath, string replacementList)
        {
            List<string> tempPaths = new List<string>();
            try
            {
                using (DatReader dat = new DatReader(File.OpenRead(srcDatPath)))
                {
                    DatBuilder builder = new DatBuilder(dat);
                    using (StreamReader sr = File.OpenText(replacementList))
                    {
                        while (!sr.EndOfStream)
                        {
                            var line = sr.ReadLine().Trim();
                            if (line.Length == 0 || line.StartsWith("#")) continue;
                            var lineSplit = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                            if (lineSplit.Length != 2)
                                throw new InvalidDataException($"Invalid line \"{line}\".");
                            if (!int.TryParse(lineSplit[0], out var imageIndex))
                                throw new InvalidDataException($"Invalid index on line \"{line}\".");

                            byte level = 1;
                            short bufferBase = 0;
                            short paletteBufferBase = 0;

                            if (imageIndex < dat.EntriesCount)
                            {
                                using (MemoryStream ms = new MemoryStream(dat.GetData(imageIndex)))
                                {
                                    TxmHeader txm = new TxmHeader();
                                    txm.Read(new BinaryReader(ms));
                                    level = (byte)(txm.Misc & 0x0f);
                                    bufferBase = txm.ImageBufferBase;
                                    paletteBufferBase = txm.ClutBufferBase;
                                }
                            }

                            string tempPath = Path.GetTempFileName();
                            tempPaths.Add(tempPath);
                            using (FileStream fs = File.Create(tempPath))
                            {
                                TxmConversion.ConvertImageToTxm(lineSplit[1], fs, level, bufferBase, paletteBufferBase);
                            }

                            builder.ReplacementEntries.Add(new DatBuilder.ReplacementEntry
                            {
                                Index = imageIndex,
                                SourceFile = tempPath
                            });
                        }
                    }
                    using (FileStream fs = File.Create(destDatPath))
                    {
                        builder.Build(fs);
                    }
                }
            }
            finally
            {
                foreach (var path in tempPaths)
                {
                    File.Delete(path);
                }
            }
        }


        static void ConvertDat(string path, string outBase = null)
        {
            if (outBase == null) outBase = Path.GetDirectoryName(path);
            Directory.CreateDirectory(outBase);
            using (Stream fs = Utils.CheckDecompress(File.OpenRead(path)))
            using (DatReader dat = new DatReader(fs))
            {
                for (int i = 0; i < dat.EntriesCount; ++i)
                {
                    using (MemoryStream subfile = new MemoryStream(dat.GetData(i)))
                    {
                        string outPath = Path.Combine(outBase, Path.GetFileName(path + $"_{i}.png"));
                        Console.WriteLine(outPath);
                        try
                        {
                            TxmConversion.ConvertTxmToPng(subfile, outPath);
                        }
                        catch (NotSupportedException)
                        {
                            File.WriteAllBytes(path + $"_{i}.txm", subfile.ToArray());
                            throw;
                        }
                    }
                }
            }
        }

        static void BulkConvertDat(string inPath, string filter, bool recursive)
        {
            foreach (var path in Directory.GetFiles(inPath, filter, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
            {
                ConvertDat(path);
            }
        }

        static void ConvertTxm(string path, string outPath = null)
        {
            using (Stream fs = Utils.CheckDecompress(File.OpenRead(path)))
            {
                if (outPath == null) outPath = Path.ChangeExtension(path, ".png");
                TxmConversion.ConvertTxmToPng(fs, outPath);
            }
        }

        static void BulkConvertTxm(string inPath, string filter, bool recursive)
        {
            foreach (var path in Directory.GetFiles(inPath, filter, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
            {
                string outPath = Path.ChangeExtension(path, ".png");
                Console.WriteLine(outPath);
                ConvertTxm(path);
            }
        }

        static void BulkConvertAtx(string inPath, string filter, bool recursive)
        {
            foreach (var path in Directory.GetFiles(inPath, filter, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
            {
                Console.WriteLine(path);
                using (var fs = Utils.CheckDecompress(File.OpenRead(path)))
                {
                    var tex = new AlTexture();
                    tex.Read(new BinaryReader(fs));

                    var img = new AlImage();
                    using (Stream ms = Utils.CheckDecompress(new MemoryStream(tex.ImageData)))
                    {
                        img.Read(new BinaryReader(ms));
                        ConvertAig(img, path);
                    }
                }
            }
        }

        static void BulkConvertAtxMulti(string inPath, string filter, bool recursive)
        {
            foreach (var path in Directory.GetFiles(inPath, filter, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
            {
                using (var fs = Utils.CheckDecompress(File.OpenRead(path)))
                {
                    var tex = new AlTexture();
                    tex.Read(new BinaryReader(fs));

                    if (tex.ChildTextures.Count < 2) continue;
                    Console.WriteLine(path);

                    var img = new AlImage();
                    using (Stream ms = Utils.CheckDecompress(new MemoryStream(tex.ImageData)))
                    {
                        img.Read(new BinaryReader(ms));
                        if (img.PixelFormat != "BGRA")
                        {
                            Console.WriteLine("Not BGRA, skipping");
                            continue;
                        }

                        using (MemoryStream ims = new MemoryStream(img.Mipmaps[0]))
                        using (var imgConv = PngConverter.ConvertBgra32(new BinaryReader(ims), (int)img.Width, (int)img.Height))
                        {
                            foreach (var child in tex.ChildTextures)
                            {
                                // First mip only
                                var bounds = child.Bounds[0];
                                using (var childImage = new Image<Bgra32>(bounds.W, bounds.H))
                                {
                                    childImage.Mutate(ctx => ctx.DrawImage(imgConv, new Point(-bounds.X, -bounds.Y), 1f));
                                    childImage.SaveAsPng($"{Path.ChangeExtension(path, null)}_{child.Id:x8}.png");
                                }
                            }
                        }
                    }
                }
            }
        }

        static void BulkConvertAtxToPng(string inPath, string filter, bool recursive)
        {
            foreach (var path in Directory.GetFiles(inPath, filter, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
            {
                Console.WriteLine(path);
                string fileExtension;

                // Export the texture (probably KTX)
                using (var fs = Utils.CheckDecompress(File.OpenRead(path)))
                {
                    var tex = new AlTexture();
                    tex.Read(new BinaryReader(fs));

                    var img = new AlImage();
                    using (Stream ms = Utils.CheckDecompress(new MemoryStream(tex.ImageData)))
                    {
                        img.Read(new BinaryReader(ms));
                        fileExtension = ConvertAig(img, path);
                    }
                }

                if (fileExtension != ".pkm") continue;

                // Convert main texture to PNG
                string mainPath = Path.ChangeExtension(path, fileExtension);
                var startInfo = new ProcessStartInfo(ETC1TOOL_PATH);
                startInfo.ArgumentList.Add(mainPath);
                startInfo.ArgumentList.Add("--decode");
                startInfo.CreateNoWindow = false;
                startInfo.UseShellExecute = false;
                Process.Start(startInfo).WaitForExit();

                // Convert alpha texture if present
                string altPath = $"{Path.ChangeExtension(path, null)}_alt{fileExtension}";
                if (File.Exists(altPath))
                {
                    startInfo = new ProcessStartInfo(ETC1TOOL_PATH);
                    startInfo.ArgumentList.Add(altPath);
                    startInfo.ArgumentList.Add("--decode");
                    startInfo.CreateNoWindow = false;
                    startInfo.UseShellExecute = false;
                    Process.Start(startInfo).WaitForExit();

                    var mainPngPath = Path.ChangeExtension(mainPath, ".png");
                    var altPngPath = Path.ChangeExtension(altPath, ".png");

                    using (var mainImg = Image.Load<Rgb24>(mainPngPath))
                    using (var altImg = Image.Load<Rgb24>(altPngPath))
                    {
                        using (var mergedImg = MergeAlpha(mainImg, altImg))
                        using (FileStream newFs = File.Create(mainPngPath))
                        {
                            mergedImg.SaveAsPng(newFs);
                        }
                    }

                    File.Delete(altPngPath);
                    File.Delete(altPath);
                }

                File.Delete(mainPath);
            }
        }

        static Image<Rgba32> MergeAlpha(Image<Rgb24> main, Image<Rgb24> alpha)
        {
            var newImg = new Image<Rgba32>(main.Width, main.Height);
            for (int y = 0; y < newImg.Height; ++y)
            {
                var newRow = newImg.GetPixelRowSpan(y);
                var mainRow = main.GetPixelRowSpan(y);
                var alphaRow = alpha.GetPixelRowSpan(y);
                for (int x = 0; x < newImg.Width; ++x)
                {
                    var mainPix = mainRow[x];
                    var alphaPix = alphaRow[x];
                    newRow[x] = new Rgba32(mainPix.R, mainPix.G, mainPix.B, alphaPix.R);
                }
            }
            return newImg;
        }

        static void BulkConvertAig(string inPath, string filter, bool recursive)
        {
            foreach (var path in Directory.GetFiles(inPath, filter, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
            {
                Console.WriteLine(path);
                using (var fs = Utils.CheckDecompress(File.OpenRead(path)))
                {
                    var img = new AlImage();
                    img.Read(new BinaryReader(fs));
                    ConvertAig(img, path);
                }
            }
        }

        static string ConvertAig(AlImage img, string path)
        {
            foreach (var converter in imageConverters)
            {
                if (converter.CanConvert(img.PixelFormat))
                {
                    string outPath = Path.ChangeExtension(path, converter.FileExtension);
                    using (FileStream ofs = File.Create(outPath))
                    {
                        converter.ConvertFromAl(img, ofs);
                    }
                    if (converter.HasAlternativeFile(img))
                    {
                        outPath = Path.ChangeExtension(path, null);
                        outPath = $"{outPath}_alt{converter.FileExtension}";
                        using (FileStream ofs = File.Create(outPath))
                        {
                            converter.ConvertFromAlAlt(img, ofs);
                        }
                    }
                    return converter.FileExtension;
                }
            }

            throw new Exception($"Cannot find converter for {img.PixelFormat}");
        }
    }
}
