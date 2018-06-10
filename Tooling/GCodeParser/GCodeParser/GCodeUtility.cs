using GCode.Interpreter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GCode.Utility
{
    internal static class ObjectExtenions
    {
        public static bool IsNumeric(this object objToTest)
        {
            return (objToTest.GetType() == typeof(int) ||
                objToTest.GetType() == typeof(double) ||
                objToTest.GetType() == typeof(double?) ||
                objToTest.GetType() == typeof(MachineVariable));
        }

        public static double? NormaliseNumeric(this object objToTest)
        {
            if (!objToTest.IsNumeric())
                throw new Exception("Can't normalise non-numeric type");
            if (objToTest.GetType() == typeof(MachineVariable))
                return ((MachineVariable)objToTest).Value;
            else if (objToTest == null)
                return null;
            else return Convert.ToDouble(objToTest);
        }
    }

    internal sealed class NumericOperands
    {
        public double? Left { get; set; }
        public double? Right { get; set; }
    }

    public class GCodeException : Exception
    {
        public int Line { get; set; }
        public int Column { get; set; }
        public string Program { get; set; }

        public GCodeException()
        {
        }

        public GCodeException(string message, string program, int line, int col)
            : base($"Error in {program}: Line {line}, col {col}. {message}")
        {
            Line = line;
            Column = col;
            Program = program;
        }

        public GCodeException(string message, string program, int line)
            : base($"Error in {program}: Line {line}. {message}")
        {
        }

        public GCodeException(string message, string program, int line, int col, Exception inner)
            : base($"Error in {program}: Line {line}, col {col}. {message}", inner)
        {
        }

        public GCodeException(string message, string program, int line, Exception inner)
            : base($"Error in {program}: Line {line}. {message}", inner)
        {
        }
    }

    // class to represent machine variables during interpretation
    internal sealed class MachineVariable
    {
        private IMachineVariableTable _varTable;
        private uint _varIdx;

        public MachineVariable(IMachineVariableTable varTable, uint varIdx)
        {
            _varTable = varTable;
            _varIdx = varIdx;
        }

        public double? Value
        {
            get
            {
                return _varTable[_varIdx];
            }
            set
            {
                _varTable[_varIdx] = value;
            }
        }

        public static implicit operator double? (MachineVariable mv)
        {
            return mv.Value;
        }
    }
}
