using System.Runtime.InteropServices;
using System.Text;

namespace KLineLogger
{
    public enum J2534Err : uint
    {
        STATUS_NOERROR = 0,
        ERR_NOT_SUPPORTED = 1,
        ERR_INVALID_CHANNEL_ID = 2,
        ERR_INVALID_PROTOCOL_ID = 3,
        ERR_NULL_PARAMETER = 4,
        ERR_INVALID_IOCTL_VALUE = 5,
        ERR_INVALID_FLAGS = 6,
        ERR_FAILED = 7,
        ERR_DEVICE_NOT_CONNECTED = 8,
        ERR_TIMEOUT = 9,
        ERR_INVALID_MSG = 10,
        ERR_INVALID_TIME_INTERVAL = 11,
        ERR_EXCEEDED_LIMIT = 12,
        ERR_INVALID_MSG_ID = 13,
        ERR_DEVICE_IN_USE = 14,
        ERR_INVALID_IOCTL_ID = 15,
        ERR_BUFFER_EMPTY = 16,
        ERR_BUFFER_FULL = 17,
        ERR_BUFFER_OVERFLOW = 18,
        ERR_PIN_INVALID = 19,
        ERR_CHANNEL_IN_USE = 20,
        ERR_MSG_PROTOCOL_ID = 21,
        ERR_INVALID_FILTER_ID = 22,
        ERR_NO_FLOW_CONTROL = 23,
        ERR_NOT_UNIQUE = 24,
        ERR_INVALID_BAUDRATE = 25,
        ERR_INVALID_DEVICE_ID = 26
    }

    public enum Protocol : uint
    {
        J1850VPW = 1,
        J1850PWM = 2,
        ISO9141 = 3,
        ISO14230 = 4,  // KWP2000
        CAN = 5,
        ISO15765 = 6,
        SCI_A_ENGINE = 7,
        SCI_A_TRANS = 8,
        SCI_B_ENGINE = 9,
        SCI_B_TRANS = 10
    }

    [Flags]
    public enum ConnectFlags : uint
    {
        NONE = 0,
        CAN_29BIT_ID = 1,
        ISO15765_29BIT_ID = 1,
        CAN_ID_BOTH = 2,
        ISO15765_FAST_INIT = 4
    }

    [Flags]
    public enum RxStatus : uint
    {
        NONE = 0,
        TX_MSG_TYPE = 1,
        START_OF_MESSAGE = 2,
        RX_BREAK = 4,
        TX_INDICATION = 8,
        ISO15765_PADDING_ERROR = 16,
        ISO15765_EXT_ADDR = 32
    }

    // Согласно J2534 стандарту - структура должна быть точно такой
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PassThruMsg
    {
        public uint ProtocolID;     // 4 bytes
        public uint RxStatus;       // 4 bytes  
        public uint TxFlags;        // 4 bytes
        public uint Timestamp;      // 4 bytes
        public uint DataSize;       // 4 bytes
        public uint ExtraDataIndex; // 4 bytes
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4128)]
        public byte[] Data;         // 4128 bytes

        public PassThruMsg()
        {
            ProtocolID = 0;
            RxStatus = 0;
            TxFlags = 0;
            Timestamp = 0;
            DataSize = 0;
            ExtraDataIndex = 0;
            Data = new byte[4128];
        }
    }

    internal static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
    }
    // SCONFIG structure for device configuration
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SCONFIG
    {
        public uint Parameter;
        public uint Value;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SCONFIG_LIST
    {
        public uint NumOfParams;
        public IntPtr ConfigPtr; // Pointer to SCONFIG array
    }

    // Ioctl parameters
    public enum IoctlID : uint
    {
        GET_CONFIG = 1,
        SET_CONFIG = 2,
        READ_VBATT = 3,
        FIVE_BAUD_INIT = 4,
        FAST_INIT = 5,
        CLEAR_TX_BUFFER = 6,
        CLEAR_RX_BUFFER = 7,
        CLEAR_PERIODIC_MSGS = 8,
        CLEAR_MSG_FILTERS = 9,
        CLEAR_FUNCT_MSG_LOOKUP_TABLE = 10,
        ADD_TO_FUNCT_MSG_LOOKUP_TABLE = 11,
        DELETE_FROM_FUNCT_MSG_LOOKUP_TABLE = 12,
        READ_PROG_VOLTAGE = 13
    }

    // Protocol flags
    public enum ProtocolFlags : uint
    {
        ISO9141_NO_CHECKSUM = 1,
        ISO9141_K = 2,
        ISO9141_L = 4
    }

    // Configuration parameters
    public enum ConfigParam : uint
    {
        DATA_RATE = 0x01,
        LOOPBACK = 0x03,
        NODE_ADDRESS = 0x04,
        NETWORK_LINE = 0x05,
        P1_MIN = 0x06,
        P1_MAX = 0x07,
        P2_MIN = 0x08,
        P2_MAX = 0x09,
        P3_MIN = 0x0A,
        P3_MAX = 0x0B,
        P4_MIN = 0x0C,
        P4_MAX = 0x0D,
        W1 = 0x0E,
        W2 = 0x0F,
        W3 = 0x10,
        W4 = 0x11,
        W5 = 0x12,
        TIDLE = 0x13,
        TINIL = 0x14,
        TWUP = 0x15,
        PARITY = 0x16,
        BIT_SAMPLE_POINT = 0x17,
        SYNC_JUMP_WIDTH = 0x18,
        W0 = 0x19,
        T1_MAX = 0x1A,
        T2_MAX = 0x1B,
        T3_MAX = 0x1C,
        T4_MAX = 0x1D,
        T5_MAX = 0x1E,
        ISO15765_BS = 0x1F,
        ISO15765_STMIN = 0x20
    }

    public enum Parity : uint
    {
        NO_PARITY = 0,
        ODD_PARITY = 1,
        EVEN_PARITY = 2
    }
    // Добавьте эти enum в J2534Types.cs

    // Флаги для подключения Mongoose
    [Flags]
    public enum MongooseFlags : uint
    {
        NONE = 0,
        DT_SNIFF_MODE = 0x00000001,
        K_LINE_ONLY = 0x00000002,
        CHECKSUM_DISABLED = 0x00000004,
        ISO9141_NO_CHECKSUM = 0x00000004, // Алиас для совместимости
        CAN_29BIT_ID = 0x00000100,
        ISO15765_29BIT_ID = 0x00000100
    }
}