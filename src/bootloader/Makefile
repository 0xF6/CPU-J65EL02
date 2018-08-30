CA=ca65
LD=ld65

all: bootloader

bootloader: bootloader.o
	$(LD) -C symon.config -vm -m bootloader.map -o bootloader.rom bootloader.o

bootloader.o:
	$(CA) --listing bootloader.lst -o bootloader.o bootloader.asm

clean:
	rm -f *.o *.rom *.map *.lst