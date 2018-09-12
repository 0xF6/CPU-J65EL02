namespace vm.components
{
    using System.Drawing;
    using cpu;
    using devices;
    using RC.Framework.Screens;

    public class Instructor : Component
    {
        public Instructor(CPU cpu) : base(cpu) { }

        /// <summary>
        /// Add with Carry, used by all addressing mode implementations of ADC.
        /// As a side effect, this will set the overflow and carry flags as needed.
        /// </summary>
        /// <param name="acc">
        /// The current value of the accumulator
        /// </param>
        /// <param name="operand">
        /// The operand
        /// </param>
        /// <returns>
        /// The sum of the accumulator and the operand
        /// </returns>
        public int ADC(int acc, int operand)
        {
            ou($"adc 0x{acc};op->0x{operand}");
            var neg = getScreen().negativeMWidth();
            var mask = getScreen().maskMWidth();
            var result = (operand & mask) + (acc & mask) + getCPU().CarryBit;
            var carry = (operand & (neg - 1)) + (acc & (neg - 1)) + getCPU().CarryBit;
            getCPU().carryFlag = (result & (mask + 1)) != 0;
            setOverflowFlag(getState().carryFlag ^ ((carry & neg) != 0));
            result &= mask;
            setArithmeticFlags(result, false);
            ou($"sbc-> 0x{result}");
            return result;
        }
        /// <summary>
        /// Add with Carry (BCD).
        /// </summary>
        public int ADCDecimal(int acc, int operand)
        {
            ou($"adc-d |> 0x{acc};op->0x{operand}");
            var l = (acc & 0x0f) + (operand & 0x0f) + getCPU().CarryBit;
            if ((l & getScreen().maskMWidth()) > 9)
                l += 6;
            var h = (acc >> 4) + (operand >> 4) + (l > 15 ? 1 : 0);
            if ((h & getScreen().maskMWidth()) > 9)
                h += 6;
            var result = (l & 0x0f) | (h << 4);
            result &= getScreen().maskMWidth();
            getCPU().carryFlag = h > 15;
            setZeroFlag(result == 0);
            setOverflowFlag(false); // BCD never sets overflow flag

            if (getCPU().IsLight)
                setNegativeFlag(false); // BCD is never negative on NMOS 6502
            else
                getState().negativeFlag = (result & 0x80) != 0; // N Flag is valid on CMOS 6502/65816
            ou($"adc-d->  0x{result}");
            return result;
        }
        /// <summary>
        /// Common code for Subtract with Carry.  Just calls ADC of the
        /// one's complement of the operand.  This lets the N, V, C, and Z
        /// flags work out nicely without any additional logic.
        /// </summary>
        public int sbc(int acc, int operand)
        {
            var result = ADC(acc, ~operand);
            setArithmeticFlags(result, false);
            ou($"sbc 0x{acc};op->0x{operand}");
            return result;
        }

        public int sbcDecimal(int acc, int operand)
        {
            ou($"sbc-d |> 0x{acc};op->0x{operand}");
            var l = (acc & 0x0f) - (operand & 0x0f) - (getState().carryFlag ? 0 : 1);
            if ((l & 0x10) != 0)
                l -= 6;
            var h = (acc >> 4) - (operand >> 4) - ((l & 0x10) != 0 ? 1 : 0);
            if ((h & 0x10) != 0)
                h -= 6;
            var result = (l & 0x0f) | (h << 4) & getScreen().maskMWidth();
            getCPU().carryFlag = (h &  getScreen().maskMWidth()) < 15;
            setZeroFlag(result == 0);
            setOverflowFlag(false); // BCD never sets overflow flag

            if (getCPU().IsLight)
                setNegativeFlag(false); // BCD is never negative on NMOS 6502
            else
                getState().negativeFlag = (result & 0x80) != 0; // N Flag is valid on CMOS 6502/65816

            getState().negativeFlag = (result & getScreen().negativeMWidth()) != 0; // N Flag is valid on CMOS 6502/65816
            ou($"sbc-d-> 0x{result}");
            return result & getScreen().maskMWidth();
        }

        /// <summary>
        /// Compare two values, and set carry, zero, and negative flags
        /// appropriately.
        /// </summary>
        public void cmp(int reg, int operand, bool x = false)
        {
            var tmp = (reg - operand) & (x ? getScreen().maskXWidth() : getScreen().maskMWidth());
            getCPU().carryFlag = reg >= operand;
            setZeroFlag(tmp == 0);
            setNegativeFlag((tmp & (x ? getScreen().negativeXWidth() 
                                 : getScreen().negativeMWidth())) != 0); // Negative bit set
            ou($"cmp |> 0x{reg};op->0x{operand},{x}");
        }
        /// <summary>
        /// Rotates the given value right by one bit, setting bit 7 to the value
        /// of the carry flag, and setting the carry flag to the original value
        /// of bit 1.
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public int ror(int m)
        {
            var result = ((m >> 1) | (getCPU().CarryBit << (getState().mWidthFlag ? 7 : 15))) & getScreen().maskMWidth();
            getCPU().carryFlag =  ((m & 0x01) != 0);
            ou($"ror |> 0x{result}");
            return result;
        }

        public void mul(int value)
        {
            ou($"mul |> 0x{value}");
            int v;
            if (getState().carryFlag)
                v = (short)value * (short)getState().A;
            else
                v = (value & 0xffff) * (getState().A & 0xffff);
            
            getState().A = v & getScreen().maskMWidth();
            getState().D = ((v >> (getState().mWidthFlag ? 8 : 16)) & getScreen().maskMWidth());
            getState().negativeFlag = v < 0;
            getState().zeroFlag = v == 0;
            getState().overflowFlag = (getState().D != 0) && (getState().D != getScreen().maskMWidth());
            ou($"mul->V 0x{v};A->0x{getState().A};D->0x{getState().D}");
        }

        public void div(int value)
        {
            ou($"div |> 0x{value}");
            if (value == 0)
            {
                getState().A = 0;
                getState().D = 0;
                getState().overflowFlag = true;
                getState().zeroFlag = false;
                getState().negativeFlag = false;
                ou($"div %rst");
                return;
            }
            int q;
            if (getState().carryFlag)
            {
                q = (short)getState().D << (getState().mWidthFlag ? 8 : 16) | getState().A;
                value = (short)value;
            }
            else
            {
                q = (getState().D & 0xffff) << (getState().mWidthFlag ? 8 : 16) | getState().A;
            }
            ou($"div $$ 0x{q} | 0x{value}");
            getState().D = q % value & getScreen().maskMWidth();
            ou($"state->d%0x{getState().D}");
            q /= value;
            ou($"div->q/v%0x{q}");
            getState().A = q & getScreen().maskMWidth();
            ou($"state->a%0x{getState().A}");
            if (getState().carryFlag)
                getState().overflowFlag = (q > getScreen().negativeMWidth() - 1) || (q < getScreen().negativeMWidth());
            else
                getState().overflowFlag = q > getScreen().negativeMWidth() - 1;
            getState().zeroFlag = getState().A == 0;
            getState().negativeFlag = q < 0;
            ou($"asl <|;");
        }

        /// <summary>
        /// Shifts the given value left by one bit, and sets the carry
        /// flag to the high bit of the initial value.
        /// </summary>
        /// <param name="m">
        /// The value to shift left.
        /// </param>
        /// <returns>
        /// left shifted value (m * 2).
        /// </returns>
        public int asl(int m)
        {
            getCPU().carryFlag = ((m & getScreen().negativeMWidth()) != 0);
            var result = (m << 1) & getScreen().maskMWidth();
            ou($"asl |> 0x{m:X4} ->> 0x{result}");
            return result;
        }
        /// <summary>
        /// Shifts the given value right by one bit, filling with zeros,
        /// and sets the carry flag to the low bit of the initial value.
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public int lsr(int m)
        {
            getCPU().carryFlag = ((m & 0x01) != 0);
            var result = (m & getScreen().maskMWidth()) >> 1;
            ou($"lsr |> 0x{m:X4} ->> 0x{result}");
            return result;
        }
        /// <summary>
        /// Rotates the given value left by one bit, setting bit 0 to the value
        /// of the carry flag, and setting the carry flag to the original value
        /// of bit 7.
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public int rol(int m)
        {
            var result = ((m << 1) | getCPU().CarryBit) & getScreen().maskMWidth();
            getCPU().carryFlag = ((m & getScreen().negativeMWidth()) != 0);
            ou($"rol |> 0x{m:X4} ->> 0x{result}");
            return result;
        }
        public void setArithmeticFlags(int reg, bool? x = null)
        {
            if (x == null)
            {
                getState().zeroFlag = (reg == 0);
                getState().negativeFlag = (reg & 0x80) != 0;
            }
            else
            {
                getState().zeroFlag = (reg == 0);
                getState().negativeFlag = (reg & (x.Value ? getScreen().negativeXWidth()
                                               : getScreen().negativeMWidth())) != 0;
            }
            
        }
        public void setNegativeFlag(bool negativeFlag) => getState().negativeFlag = negativeFlag;
        public void clearNegativeFlag() => getState().negativeFlag = false;
        public void setZeroFlag(bool zeroFlag) => getState().zeroFlag = zeroFlag;
        public void setOverflowFlag(bool overflowFlag) => getState().overflowFlag = overflowFlag;

        private void ou(object s) => Log.nf(s, RCL.Wrap("ASM", Color.LightSeaGreen));
    }
}