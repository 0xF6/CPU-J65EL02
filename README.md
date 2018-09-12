# CPU-J65EL02
A C# implementation of the 65el02 CPU.


### Dependences for build 🔥
1. NET Framework 4.7.2 (NET Core coming soon)

### Details
- `cpu`           - a cpu impl project
- `bootloader`    - asm code for this cpu
- `dasm-cli`      - nodejs cli applicaion for compile 6502 dasm
- `screen`        - electron application screen emulator 

#### Devices info
```CSharp
- Memory:
    Address: at 0x0000 to 0x***** (not more that 0x40000) 
- Acia6551:
    Address: at 0x8800 to 0x8803
    Registers:
        Data   (DATA_REG) = 0x0 readonly
        Status (STAT_REG) = 0x1 readonly
        Command(CMND_REG) = 0x2 readonly
        Control(CTRL_REG) = 0x3 readonly
    BaudRate: from 0 to 19200
- Acia6850:
    Address: at 0x8800 to 0x8802
    Registers:
        Status  (STAT_REG) = 0x0 readonly
        Command (CTRL_REG) = 0x0 write-only
        ReadRX  (RX_REG)   = 0x1 readonly
        ReadText(TX_REG)   = 0x1 write-only
 - CRTC:
    Address: at 0x9000 to 0x9002
    Registers:
        Select(REG_SELECT) = 0x0 write-olny
        Write (REG_RW)     = 0x1 out and in
 - Bus:
    Address: at 0xFFFFFFFF to 0xFFFFFFFF (no fixed address for the classic-bus)
    Capacity: 0x10 (default)
 - RedBus:
    Address: at 0xFFFFFFFF to 0xFFFFFFFF (no fixed address for the redbus)
    Peripheral:
      Capacity: 0x100 (hardcode)
 - Wireless Display:
    Address: at 0x9500 to 0x9506
    Registers:
        Init        (REGINIT) = 0x0 write-only
        Connect     (REGCON)  = 0x1 write-olny
        WriteChar   (REGWR)   = 0x2 write-olny
        Status      (REGST)   = 0x2 readonly
        PhysPort    (PHPRT)   = 0x3 readonly
```

#### Support Instructions   
```elixir
- adc (at 0x69 in Execute)
- sbc (at 0xE9 in Execute)
- cmp (at 0xC0, 0xC4, 0xCC, 0xC9, 0xE0, 0xE4, 0xEC in Execute)
- ror (at 0x6A, 0x66, 0x6E, 0x76, 0x7E in Execute)
- mul (at 0x0F, 0x1F, 0x2F, 0x3F in Execute)
- div (at 0x4F, 0x5F, 0x6F, 0x7F in Execute)
- asl (at 0x0A, 0x06, 0x0E, 0x16, 0x1E in Execute)
- lsr (at 0x4A, 0x46, 0x4E, 0x56, 0x5E in Execute)
- rol (at 0x2A, 0x26, 0x2E, 0x36, 0x3E in Execute)

- xadr   (at 0x7 in cycle-address)
- yadr   (at 0x7 in cycle-address)
- zpxadr (at 0x5 in cycle-address)
- zpyadr (at 0x5 in cycle-address)
```

#### Sizes of instructions
```CSharp
1, 2, 1, 2, 2, 2, 2, 2, 1, 2, 1, 1, 3, 3, 3, 2,   // 0x00-0x0f
2, 2, 2, 2, 2, 2, 2, 2, 1, 3, 1, 1, 3, 3, 3, 2,   // 0x10-0x1f
3, 2, 1, 2, 2, 2, 2, 2, 1, 2, 1, 1, 3, 3, 3, 3,   // 0x20-0x2f
2, 2, 2, 2, 2, 2, 2, 2, 1, 3, 1, 1, 3, 3, 3, 3,   // 0x30-0x3f
1, 2, 1, 2, 3, 2, 2, 2, 1, 2, 1, 1, 3, 3, 3, 2,   // 0x40-0x4f
2, 2, 2, 2, 2, 2, 2, 2, 1, 3, 1, 1, 1, 3, 3, 2,   // 0x50-0x5f
1, 2, 2, 2, 2, 2, 2, 2, 1, 2, 1, 1, 3, 3, 3, 3,   // 0x60-0x6f
2, 2, 2, 2, 2, 2, 2, 2, 1, 3, 1, 1, 3, 3, 3, 3,   // 0x70-0x7f
2, 2, 2, 2, 2, 2, 2, 2, 1, 2, 1, 1, 3, 3, 3, 1,   // 0x80-0x8f
2, 2, 2, 2, 2, 2, 2, 2, 1, 3, 1, 1, 3, 3, 3, 1,   // 0x90-0x9f
2, 2, 2, 2, 2, 2, 2, 2, 1, 2, 1, 1, 3, 3, 3, 1,   // 0xa0-0xaf
2, 2, 2, 2, 2, 2, 2, 2, 1, 3, 1, 1, 3, 3, 3, 1,   // 0xb0-0xbf
2, 2, 2, 2, 2, 2, 2, 2, 1, 2, 1, 1, 3, 3, 3, 1,   // 0xc0-0xcf
2, 2, 2, 2, 2, 2, 2, 2, 1, 3, 1, 1, 1, 3, 3, 1,   // 0xd0-0xdf
2, 2, 2, 2, 2, 2, 2, 2, 1, 2, 1, 1, 3, 3, 3, 2,   // 0xe0-0xef
2, 2, 2, 2, 3, 2, 2, 2, 1, 3, 1, 1, 3, 3, 3, 0    // 0xf0-0xff
```

#### Addressing modes
```asm
  ACC, AIX, ABS, ABX, ABY,
  ASP, ABR, IMM, IMP, IND,
  XIN, INY, ISY, IRY, REL,
  ZPG, ZPR, ZPX, ZPY, ZPI,
  NUL
```

### WSoD  (White Screen of Dead)

WSoD displays information about the last write\read memory, error information, as well as cpu-state, cpu-flags and devices that are currently in the bus. 

![image](https://user-images.githubusercontent.com/13326808/44694240-0e24fa00-aa75-11e8-8a41-4c035b71ea48.png)
