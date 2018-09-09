
; set processor type
        processor 6502
; set input point
        org $0300 

;; declare variables
string .byte "Reei gaaay ", 0
;; <> - io driver
iobase   = $8800
iostatus = iobase + 1
iocmd    = iobase + 2
ioctrl   = iobase + 3
;; <> wireless display
iowrldev = $9500
iowrlreg = iowrldev + 1
iowrlsta = iowrldev + 2
;; SECTION 'CODE'
START
        CLI
        LDA $0b
        STA iocmd
        LDA $1a
        STA ioctrl
        LDA $00
        STA iowrldev
        LDA $00
        STA iowrlreg

INIT
        LDX $00
LOOP
        LDA iostatus
        AND $10
        BEQ LOOP      
        LDA iowrlsta
        AND $02
        BEQ LOOP   
        LDA string,x 
        BEQ INIT      
        STA iobase 
        INX          
        JMP LOOP  
