iobase   = $8800
iostatus = iobase + 1
iocmd    = iobase + 2
ioctrl   = iobase + 3

.segment "CODE"
.org $0300

start:  cli
        lda #$0b
        sta iocmd
        lda #$1a
        sta ioctrl

init:   ldx #$00

loop:   lda iostatus
        and #$10
        beq loop       
        lda string,x 
        beq init      
        sta iobase  

        inx          
        jmp loop  

string: .byte "Reei gaaay ", 0