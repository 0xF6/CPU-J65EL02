
; set processor type
        processor 6502
; set input point
        org $0299 ; at dasm compiler, need set start point at 0x0299 


;; <> - io driver
iobase   = $8800
iostatus = iobase + 1
iocmd    = iobase + 2
ioctrl   = iobase + 3
;; <> wireless display
;iowrldev = $9500
;iowrlreg = iowrldev + 1
;iowrlsta = iowrldev + 2
;; SECTION 'CODE'
START:
        CLI
        LDA #$0b
        STA iocmd
        LDA #$1a
        STA ioctrl
        ;LDA $00
        ;STA iowrldev ; warm up device
        ;LDA $00
        ;STA iowrlreg ; load cnnection shell

INIT:
        LDX #$00
LOOP:
        LDA iostatus 
        AND #$11
        BEQ LOOP      ; await status - LOADED
        ;LDA iowrlsta
        ;AND $02
        ;BEQ LOOP   
        LDA string,x ; write char at index
        BEQ INIT      
        STA iobase 
        INX          
        JMP LOOP  

;; declare variables
string .byte "elis gaaay ", 0