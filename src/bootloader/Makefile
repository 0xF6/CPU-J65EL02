CA=ca65
LD=ld65

all: bootloader

bootloader: bootloader.o
	$(LD) -C 6502.config -vm -m bootloader.map -o bootloader.elf bootloader.o
bootloader.o:
	$(CA) --listing bootloader.lst -o bootloader.o bootloader.asm

clean:
	rm -f *.o *.elf *.map *.lst
