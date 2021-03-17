using LibDgf;
using LibDgf.Aqualead.Image;
using LibDgf.Aqualead.Image.Conversion;
using LibDgf.Aqualead.Texture;
using LibDgf.Dat;
using LibDgf.Font;
using LibDgf.Graphics;
using LibDgf.Graphics.Mesh;
using LibDgf.Mesh;
using LibDgf.Txm;
using McMaster.Extensions.CommandLineUtils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Path = System.IO.Path;

namespace DgfTxmConvert
{
    class Program
    {
        const string ETC1TOOL_PATH = @"etc1tool.exe";

        static List<IAlImageConverter> imageConverters;

        static int Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
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

            app.Command("dump-font", config =>
            {
                config.FullName = "Dump kanji font";
                config.Description = "Extracts all characters from kanji font pack.";

                var pakPath = config.Argument("pakPath", "Path of the kanji pack").IsRequired();
                pakPath.Accepts().ExistingFile();
                var txmPath = config.Argument("txmPath", "Path of the kana TXM").IsRequired();
                txmPath.Accepts().ExistingFile();
                var outputPath = config.Argument("outputPath", "Path of directory to extract character images to");
                txmPath.Accepts().LegalFilePath();
                config.HelpOption();

                config.OnExecute(() =>
                {
                    ExtractFontPack(pakPath.Value, txmPath.Value, outputPath.Value);
                });
            });

            app.Command("extract-dat", config =>
            {
                config.FullName = "Extracts DAT";
                config.Description = "Extracts files from DAT.";

                var datPathArg = config.Argument("datPath", "Path of the DAT to extract").IsRequired();
                datPathArg.Accepts().ExistingFile();
                var outBaseArg = config.Argument("outBase", "Directory to write extracted files");
                outBaseArg.Accepts().LegalFilePath();
                config.HelpOption();

                config.OnExecute(() =>
                {
                    ExtractDat(datPathArg.Value, outBaseArg.Value);
                });
            });

            app.Command("convert-trm", config =>
            {
                config.FullName = "Converts TRM";
                config.Description = "Converts train models.";

                var trmPathArg = config.Argument("trmPath", "Path of the train model to extract").IsRequired();
                trmPathArg.Accepts().ExistingFile();
                var outBaseArg = config.Argument("outBase", "Directory to write converted files");
                outBaseArg.Accepts().LegalFilePath();
                config.HelpOption();

                config.OnExecute(() =>
                {
                    ExtractTrm(trmPathArg.Value, outBaseArg.Value);
                });
            });

            app.Command("convert-pdb", config =>
            {
                config.FullName = "Converts PDB";
                config.Description = "Converts PDB models.";

                var pdbPathArg = config.Argument("pdbPath", "Path of the model pack to extract").IsRequired();
                pdbPathArg.Accepts().ExistingFile();
                var txmPathArg = config.Argument("txmPath", "Path of the associated texture pack").IsRequired();
                txmPathArg.Accepts().ExistingFile();
                var outBaseArg = config.Argument("outBase", "Directory to write converted files");
                outBaseArg.Accepts().LegalFilePath();
                var forceTexDirectOption = config.Option("--forceTexDirect", "Convert TXM as is without trying PS2 texture unpack", CommandOptionType.NoValue);
                config.HelpOption();

                config.OnExecute(() =>
                {
                    ExtractPdb(pdbPathArg.Value, txmPathArg.Value, outBaseArg.Value, forceTexDirectOption.HasValue());
                });
            });

            app.Command("convert-bg", config =>
            {
                config.FullName = "Converts sky dome";
                config.Description = "Converts sky dome.";

                var bgPathArg = config.Argument("bgPath", "Path of the sky dome pack to extract").IsRequired();
                bgPathArg.Accepts().ExistingFile();
                var outBaseArg = config.Argument("outBase", "Directory to write converted files");
                outBaseArg.Accepts().LegalFilePath();
                config.HelpOption();

                config.OnExecute(() =>
                {
                    ExtractBg(bgPathArg.Value, outBaseArg.Value);
                });
            });

            app.Command("convert-mapanim", config =>
            {
                config.FullName = "Converts mapanim files";
                config.Description = "Converts mapanim models.";

                var mapAnimPathArg = config.Argument("mapAnimPathArg", "Path of the mapanim model to extract").IsRequired();
                mapAnimPathArg.Accepts().ExistingFile();
                var outBaseArg = config.Argument("outBase", "Directory to write converted files");
                outBaseArg.Accepts().LegalFilePath();
                config.HelpOption();

                config.OnExecute(() =>
                {
                    ExtractMapAnim(mapAnimPathArg.Value, outBaseArg.Value);
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

        static void ExtractDat(string datPath, string outBase)
        {
            if (outBase == null) outBase = Path.GetDirectoryName(datPath);
            using (DatReader dat = new DatReader(Utils.CheckDecompress(File.OpenRead(datPath))))
            {
                for (int i = 0; i < dat.EntriesCount; ++i)
                {
                    string outPath = Path.Combine(outBase, Path.GetFileName(datPath + $"_{i}.bin"));
                    File.WriteAllBytes(outPath, dat.GetData(i));
                }
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
                            ushort bufferBase = 0;
                            ushort paletteBufferBase = 0;

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

        static void ExtractFontPack(string pakPath, string txmPath, string basePath)
        {
            if (basePath == null) basePath = pakPath + "_extracted";
            using (Stream fs = Utils.CheckDecompress(File.OpenRead(txmPath)))
            using (Stream datFs = Utils.CheckDecompress(File.OpenRead(pakPath)))
            {
                var fontTxmHeader = new TxmHeader();
                BinaryReader br = new BinaryReader(fs);
                fontTxmHeader.Read(br);
                var palette = TxmConversion.ReadRgba32Palette(br, fontTxmHeader.ClutWidth, fontTxmHeader.ClutHeight);

                var fontPak = new FontPack();
                fontPak.Read(datFs);
                Directory.CreateDirectory(basePath);

                foreach (var ch in fontPak.Characters)
                {
                    using MemoryStream ms = new MemoryStream(fontPak[ch]);
                    BinaryReader charBr = new BinaryReader(ms);
                    using (var img = TxmConversion.ConvertTxmIndexed4bpp(charBr, 24, 22, palette))
                    {
                        img.SaveAsPng(Path.Combine(basePath, $"{ch}.png"));
                    }
                }
            }
        }

        static void ExtractTrm(string trmPath, string basePath = null)
        {
            if (basePath == null)
                basePath = Path.ChangeExtension(trmPath, null);
            else
                basePath = Path.Combine(basePath, Path.GetFileNameWithoutExtension(trmPath));

            using DatReader dat = new DatReader(Utils.CheckDecompress(File.OpenRead(trmPath)));
            // First entry is texture DAT
            using DatReader txmDat = new DatReader(new MemoryStream(dat.GetData(0)));
            using ObjConverter converter = new ObjConverter(txmDat);
            string mtlPath = basePath + ".mtl";
            string mtlName = Path.GetFileName(mtlPath);
            // Subsequent entries are train car DATs
            for (int i = 1; i < dat.EntriesCount; ++i)
            {
                using DatReader innerDat = new DatReader(new MemoryStream(dat.GetData(i)));
                // And within each train car DAT are PDBs
                for (int j = 0; j < innerDat.EntriesCount; ++j)
                {
                    using MemoryStream ms = new MemoryStream(innerDat.GetData(j));
                    BinaryReader br = new BinaryReader(ms);
                    Pdb pdb = new Pdb();
                    pdb.Read(br);
                    using StreamWriter sw = File.CreateText($"{basePath}.{i}_{j}.obj");
                    sw.WriteLine($"mtllib {mtlName}");
                    sw.WriteLine();

                    converter.ConvertObj(pdb, sw);
                }
            }

            using (StreamWriter sw = File.CreateText(mtlPath))
            {
                converter.ExportTextures(sw, basePath + ".");
            }
        }

        static void ExtractPdb(string pdbPath, string txmPath, string basePath = null, bool forceDirect = false)
        {
            if (basePath == null)
                basePath = Path.ChangeExtension(pdbPath, null);
            else
                basePath = Path.Combine(basePath, Path.GetFileNameWithoutExtension(pdbPath));

            DatReader dat = new DatReader(Utils.CheckDecompress(File.OpenRead(pdbPath)));
            using DatReader txmDat = new DatReader(Utils.CheckDecompress(File.OpenRead(txmPath)));
            using ObjConverter converter = new ObjConverter(txmDat);
            string mtlPath = basePath + ".mtl";
            string mtlName = Path.GetFileName(mtlPath);
            for (int i = 0; i < dat.EntriesCount; ++i)
            {
                using MemoryStream ms = new MemoryStream(dat.GetData(i));
                BinaryReader br = new BinaryReader(ms);
                Pdb pdb = new Pdb();
                pdb.Read(br);
                using StreamWriter sw = File.CreateText($"{basePath}.{i}.obj");
                sw.WriteLine($"mtllib {mtlName}");
                sw.WriteLine();

                converter.ConvertObj(pdb, sw);
            }

            using (StreamWriter sw = File.CreateText(mtlPath))
            {
                converter.ExportTextures(sw, basePath + ".", forceDirect);
            }
        }

        static void ExtractBg(string bgPath, string basePath = null)
        {
            if (basePath == null)
                basePath = Path.ChangeExtension(bgPath, null);
            else
                basePath = Path.Combine(basePath, Path.GetFileNameWithoutExtension(bgPath));

            DatReader dat = new DatReader(Utils.CheckDecompress(File.OpenRead(bgPath)));
            using ObjConverter converter = new ObjConverter(dat);
            string mtlPath = basePath + ".mtl";
            string mtlName = Path.GetFileName(mtlPath);
            for (int i = 0; i < 3; ++i)
            {
                using MemoryStream ms = new MemoryStream(dat.GetData(i));
                DatReader innerDat = new DatReader(ms);
                for (int j = 0; j < innerDat.EntriesCount; ++j)
                {
                    using BinaryReader br = new BinaryReader(new MemoryStream(innerDat.GetData(j)));
                    Tdb tdb = new Tdb();
                    tdb.Read(br);

                    // Remap textures (only known for Windows version, PS2 todo when files obtained)
                    if (i == 0)
                    {
                        tdb.Textures[0].DatIndex = 4;
                        tdb.Textures[1].DatIndex = 3;
                    }
                    else
                    {
                        tdb.Textures[0].DatIndex = 5;
                    }

                    using StreamWriter sw = File.CreateText($"{basePath}.{i}_{j}.obj");
                    sw.WriteLine($"mtllib {mtlName}");
                    sw.WriteLine();

                    converter.ConvertObj(tdb, sw);
                }
            }

            using (StreamWriter sw = File.CreateText(mtlPath))
            {
                converter.ExportTextures(sw, basePath + ".", true);
            }
        }

        static void ExtractMapAnim(string mapAnimPath, string basePath = null)
        {
            if (basePath == null)
                basePath = Path.ChangeExtension(mapAnimPath, null);
            else
                basePath = Path.Combine(basePath, Path.GetFileNameWithoutExtension(mapAnimPath));

            using DatReader dat = new DatReader(Utils.CheckDecompress(File.OpenRead(mapAnimPath)));
            // Second entry is texture DAT
            using DatReader txmDat = new DatReader(new MemoryStream(dat.GetData(1)));
            using ObjConverter converter = new ObjConverter(txmDat);
            string mtlPath = basePath + ".mtl";
            string mtlName = Path.GetFileName(mtlPath);
            // First entry is a collection of weird PDBs
            using DatReader pdbDat = new DatReader(new MemoryStream(dat.GetData(0)));
            for (int i = 0; i < pdbDat.EntriesCount; ++i)
            {
                using MemoryStream ms = new MemoryStream(pdbDat.GetData(i));
                // This is a DAT, but we're going to pretend it's a normal PDB
                BinaryReader br = new BinaryReader(ms);
                Pdb pdb = new Pdb();
                pdb.Read(br);
                using StreamWriter sw = File.CreateText($"{basePath}.{i}.obj");
                sw.WriteLine($"mtllib {mtlName}");
                sw.WriteLine();

                converter.ConvertObj(pdb, sw);
            }

            using (StreamWriter sw = File.CreateText(mtlPath))
            {
                converter.ExportTextures(sw, basePath + ".");
            }
        }
    }
}
