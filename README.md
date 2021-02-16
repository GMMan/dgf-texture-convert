Densha de Go! Final Texture Converter
=====================================

This program allows you to extract and inject textures for DDG Final.

Requirements
------------
Requires [LibDgf](https://github.com/GMMan/libdgf)

Usage
-----
The following commands are available. For arguments please use the `--help`
option.

- `convert-dat`: Unpacks a .DAT container of textures and converts everything
  to PNG.
- `convert-txm`: Converts a single .TXM file to PNG.
- `replace-dat`: Imports and replaces a list of images into a .DAT file.
  Requires a text file with a line for each texture to be replaced, consisting
  of the index number and the path to the corresponding image (can be relative
  to working directory).

Other
-----
There is some code for bulk convert all .DAT/.TXM files in a directory, along
with some code for Aqualead texture/image formats. Modify the program as
necessary yourself to use them.
