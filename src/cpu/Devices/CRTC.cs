namespace vm.devices
{
    using components;
    using cpu;
    using exceptions;

    public class CRTC : Device
    {
        // Memory locations in the CRTC address space
        public const int REGISTER_SELECT = 0;
        public const int REGISTER_RW = 1;

        // Registers
        public const int HORIZONTAL_DISPLAYED = 1;
        public const int VERTICAL_DISPLAYED = 6;
        public const int MODE_CONTROL = 8;
        public const int SCAN_LINE = 9;
        public const int CURSOR_START = 10;
        public const int CURSOR_END = 11;
        public const int DISPLAY_START_HIGH = 12;
        public const int DISPLAY_START_LOW = 13;
        public const int CURSOR_POSITION_HIGH = 14;
        public const int CURSOR_POSITION_LOW = 15;

        // R1 - Horizontal Displayed
        private int horizontalDisplayed;

        // R6 - Vertical Displayed
        private int verticalDisplayed;

        // R9 - Scan Lines: Number of scan lines per character, including spacing.
        private int scanLinesPerRow;

        // R10 - Cursor Start / Cursor Mode
        private int cursorStartLine;
        private bool cursorEnabled;
        private int cursorBlinkRate;

        // R11 - Cursor End
        private int cursorStopLine;

        // R12, R13 - Display Start Address: The starting address in the video RAM of the displayed page.
        private int startAddress;

        // R14, R15 - Cursor Position
        private int cursorPosition;

        // The size, in bytes, of a displayed page of characters.
        private int pageSize;

        private int currentRegister = 0;

        // Status bits
        private bool rowColumnAddressing = false;
        private bool displayEnableSkew = false;
        private bool cursorSkew = false;

        private Memory memory;


        public CRTC(int startAddress, CPU cp, Memory mem) : base(startAddress, startAddress + 2, cp)
        {
            memory = mem;

            // Defaults
            this.horizontalDisplayed = 40;
            this.verticalDisplayed = 25;
            this.scanLinesPerRow = 9;
            this.cursorStartLine = 0;
            this.cursorStopLine = 7;
            this.startAddress = 0x7000;
            this.cursorPosition = startAddress;
            this.pageSize = horizontalDisplayed * verticalDisplayed;
            this.cursorEnabled = true;
            this.cursorBlinkRate = 500;
        }

        public override void write(int address, int data)
        {
            switch (address)
            {
                case REGISTER_SELECT:
                    currentRegister = data;
                    break;
                case REGISTER_RW:
                    writeRegisterValue(data);
                    break;
            }
        }

        public override int read(int address, bool cpuAccess)
        {
            switch (address)
            {
                case REGISTER_RW:
                    switch (currentRegister)
                    {
                        case CURSOR_POSITION_LOW:
                            return cursorPosition & 0xff;
                        case CURSOR_POSITION_HIGH:
                            return cursorPosition >> 8;
                        default:
                            return 0;
                    }
                default:
                    return 0;
            }
        }
        public int getCharAtAddress(int address) 
        {
            // TODO: Row/Column addressing
            return memory.read(address, false);
        }

        private void writeRegisterValue(int data)
        {
            var oldStartAddress = startAddress;
            var oldCursorPosition = cursorPosition;

            switch (currentRegister)
            {
                case HORIZONTAL_DISPLAYED:
                    horizontalDisplayed = data;
                    pageSize = horizontalDisplayed * verticalDisplayed;
                    break;
                case VERTICAL_DISPLAYED:
                    verticalDisplayed = data;
                    pageSize = horizontalDisplayed * verticalDisplayed;
                    break;
                case MODE_CONTROL:
                    rowColumnAddressing = (data & 0x04) != 0;
                    displayEnableSkew = (data & 0x10) != 0;
                    cursorSkew = (data & 0x20) != 0;
                    break;
                case SCAN_LINE:
                    scanLinesPerRow = data;
                    break;
                case CURSOR_START:
                    cursorStartLine = data & 0x1f;
                    // Bits 5 and 6 define the cursor mode.
                    int cursorMode = (data & 0x60) >> 5;
                    switch (cursorMode)
                    {
                        case 0:
                            cursorEnabled = true;
                            cursorBlinkRate = 0;
                            break;
                        case 1:
                            cursorEnabled = false;
                            cursorBlinkRate = 0;
                            break;
                        case 2:
                            cursorEnabled = true;
                            cursorBlinkRate = 500;
                            break;
                        case 3:
                            cursorEnabled = true;
                            cursorBlinkRate = 1000;
                            break;
                    }
                    break;
                case CURSOR_END:
                    cursorStopLine = data & 0x1f;
                    break;
                case DISPLAY_START_HIGH:
                    startAddress = ((data & 0xff) << 8) | (startAddress & 0x00ff);
                    break;
                case DISPLAY_START_LOW:
                    startAddress = ((data & 0xff) | (startAddress & 0xff00));
                    break;
                case CURSOR_POSITION_HIGH:
                    cursorPosition = ((data & 0xff) << 8) | (cursorPosition & 0x00ff);
                    break;
                case CURSOR_POSITION_LOW:
                    cursorPosition = (data & 0xff) | (cursorPosition & 0xff00);
                    break;
            }

            if (startAddress + pageSize > memory.endAddress)
            {
                startAddress = oldStartAddress;
                throw new CorruptedMemoryException("Cannot draw screen starting at selected address.", this);
            }

            if (cursorPosition > memory.endAddress)
            {
                cursorPosition = oldCursorPosition;
                throw new CorruptedMemoryException("Cannot position cursor past end of memory.", this);
            }
            //notifyListeners();
        }

        public override string getName() => "CRTC";
    }
}