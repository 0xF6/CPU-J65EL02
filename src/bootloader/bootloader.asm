

;; <> - io driver
iobase   = $8800
iostatus = iobase + 1
iocmd    = iobase + 2
ioctrl   = iobase + 3


;; <> wireless display
iowrldev = $9500
iowrlreg = iowrldev + 1
iowrlsta = iowrldev + 2


.segment "CODE"
.org $0300

start:  cli
        lda $0b
        sta iocmd
        lda $1a
        sta ioctrl
        lda $00
        sta iowrldev
        lda $00
        sta iowrlreg

init:   ldx $00

loop:   LDA iostatus
        AND $10
        beq loop      
        lda iowrlsta
        and $02
        beq loop   
        lda string,x 
        beq init      
        sta iobase  

        inx          
        jmp loop  

string: .byte "Reei gaaay \n", 0