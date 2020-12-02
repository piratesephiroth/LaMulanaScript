# LaMulanaScript
A tool to decode and reencode the "script_code.dat" files of the La-Mulana remake.

Requires a text file called `fontChars.txt`, containing all the unique characters found in font00.png, in the same order.

These characters are just used as reference for the conversion.  
Take the special word "Undefined" as an example, which is made of 3 tiles.  
You can use any 3 unicode characters for the text equivalent (regular characters, not emojis).


## Notes:
* `font00.png` is a sheet containing the characters, each being 21x21 pixels in size.
* `grif.dat` contains the definitions for each of these characters, listed top to bottom and right to left.  
It's composed of 16bit unsigned integers.
First value is the glyph count.  
The remaining pairs contain the starting horizontal offset and the width.
* The {COL} command uses the CMY color model, so basically the opposite of RGB values.  
000-000-000 is white, 255-255-255 is black.
