using System.Runtime.InteropServices;
using System.Text;

namespace KLineLogger
{
    public class J2534Wrapper : IDisposable
    {
        private IntPtr _deviceId = IntPtr.Zero;
        private IntPtr _channelId = IntPtr.Zero;
        private IntPtr _libraryHandle = IntPtr.Zero;
        private bool _disposed = false;
        private string? _deviceName;
        private IntPtr _periodicMsgId = IntPtr.Zero;
        private StreamWriter? _logWriter;
        private string _logFilePath;

        // J2534 Function Delegates
        private delegate J2534Err PassThruOpenDelegate([MarshalAs(UnmanagedType.LPStr)] string pName, out IntPtr pDeviceID);
        private delegate J2534Err PassThruCloseDelegate(IntPtr DeviceID);
        private delegate J2534Err PassThruConnectDelegate(IntPtr DeviceID, uint ProtocolID, uint Flags, uint Baudrate, out IntPtr pChannelID);
        private delegate J2534Err PassThruDisconnectDelegate(IntPtr ChannelID);
        private delegate J2534Err PassThruReadMsgsDelegate(IntPtr ChannelID, IntPtr pMsg, ref uint pNumMsgs, uint Timeout);
        private delegate J2534Err PassThruWriteMsgsDelegate(IntPtr ChannelID, IntPtr pMsg, ref uint pNumMsgs, uint Timeout);
        private delegate J2534Err PassThruStartPeriodicMsgDelegate(IntPtr ChannelID, IntPtr pMsg, out IntPtr pMsgID, uint TimeInterval);
        private delegate J2534Err PassThruStopPeriodicMsgDelegate(IntPtr ChannelID, IntPtr MsgID);
        private delegate J2534Err PassThruStartMsgFilterDelegate(IntPtr ChannelID, uint FilterType, IntPtr pMaskMsg, IntPtr pPatternMsg, IntPtr pFlowControlMsg, out IntPtr pFilterID);
        private delegate J2534Err PassThruReadVersionDelegate(IntPtr DeviceID, [MarshalAs(UnmanagedType.LPStr)] StringBuilder pApiVersion, [MarshalAs(UnmanagedType.LPStr)] StringBuilder pFirmwareVersion, [MarshalAs(UnmanagedType.LPStr)] StringBuilder pDllVersion);
        private delegate J2534Err PassThruGetLastErrorDelegate([MarshalAs(UnmanagedType.LPStr)] StringBuilder pErrorDescription);
        private delegate J2534Err PassThruIoctlDelegate(IntPtr Handle, uint IoctlID, IntPtr pInput, IntPtr pOutput);

        private PassThruOpenDelegate _PassThruOpen;
        private PassThruCloseDelegate _PassThruClose;
        private PassThruConnectDelegate _PassThruConnect;
        private PassThruDisconnectDelegate _PassThruDisconnect;
        private PassThruReadMsgsDelegate _PassThruReadMsgs;
        private PassThruWriteMsgsDelegate _PassThruWriteMsgs;
        private PassThruStartPeriodicMsgDelegate _PassThruStartPeriodicMsg;
        private PassThruStopPeriodicMsgDelegate _PassThruStopPeriodicMsg;
        private PassThruStartMsgFilterDelegate _PassThruStartMsgFilter;
        private PassThruReadVersionDelegate _PassThruReadVersion;
        private PassThruGetLastErrorDelegate _PassThruGetLastError;
        private PassThruIoctlDelegate _PassThruIoctl;

        public J2534Wrapper(string libraryPath)
        {
            LoadLibrary(libraryPath);
            InitializeLogging();
        }

        private void InitializeLogging()
        {
            try
            {
                string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                _logFilePath = Path.Combine(logDirectory, $"kline_log_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                _logWriter = new StreamWriter(_logFilePath, append: true);

                LogToFile($"=== K-Line Session Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                LogToFile($"Device Library: {Path.GetFileName(_deviceName)}");
                Console.WriteLine($"📝 Log file: {_logFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠ Log initialization error: {ex.Message}");
            }
        }

        private void LogToFile(string message)
        {
            try
            {
                _logWriter?.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}");
                _logWriter?.Flush();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠ Log write error: {ex.Message}");
            }
        }

        private void LoadLibrary(string libraryPath)
        {
            if (!File.Exists(libraryPath))
                throw new FileNotFoundException($"J2534 DLL not found: {libraryPath}");

            _libraryHandle = NativeMethods.LoadLibrary(libraryPath);
            if (_libraryHandle == IntPtr.Zero)
                throw new Exception($"Failed to load library: {libraryPath}");

            // Определяем имя устройства по пути библиотеки
            _deviceName = Path.GetFileNameWithoutExtension(libraryPath)?.ToLower();

            // Загружаем функции
            _PassThruOpen = GetFunction<PassThruOpenDelegate>("PassThruOpen");
            _PassThruClose = GetFunction<PassThruCloseDelegate>("PassThruClose");
            _PassThruConnect = GetFunction<PassThruConnectDelegate>("PassThruConnect");
            _PassThruDisconnect = GetFunction<PassThruDisconnectDelegate>("PassThruDisconnect");
            _PassThruReadMsgs = GetFunction<PassThruReadMsgsDelegate>("PassThruReadMsgs");
            _PassThruWriteMsgs = GetFunction<PassThruWriteMsgsDelegate>("PassThruWriteMsgs");
            _PassThruStartPeriodicMsg = GetFunction<PassThruStartPeriodicMsgDelegate>("PassThruStartPeriodicMsg");
            _PassThruStopPeriodicMsg = GetFunction<PassThruStopPeriodicMsgDelegate>("PassThruStopPeriodicMsg");
            _PassThruStartMsgFilter = GetFunction<PassThruStartMsgFilterDelegate>("PassThruStartMsgFilter");
            _PassThruReadVersion = GetFunction<PassThruReadVersionDelegate>("PassThruReadVersion");
            _PassThruGetLastError = GetFunction<PassThruGetLastErrorDelegate>("PassThruGetLastError");
            _PassThruIoctl = GetFunction<PassThruIoctlDelegate>("PassThruIoctl");
        }

        private T GetFunction<T>(string functionName) where T : Delegate
        {
            var address = NativeMethods.GetProcAddress(_libraryHandle, functionName);
            if (address == IntPtr.Zero)
                throw new Exception($"Function {functionName} not found in library");
            return (T)Marshal.GetDelegateForFunctionPointer(address, typeof(T));
        }

        public bool Initialize()
        {
            try
            {
                var result = _PassThruOpen(null, out _deviceId);
                if (result == J2534Err.STATUS_NOERROR && _deviceId != IntPtr.Zero)
                {
                    Console.WriteLine($"✓ Device opened: 0x{_deviceId:X8}");
                    LogToFile($"DEVICE_OPENED - Handle: 0x{_deviceId:X8}");
                    return true;
                }
                Console.WriteLine($"✗ PassThruOpen failed: {result}");
                LogToFile($"DEVICE_OPEN_FAILED - Error: {result}");
                ReportError();
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Initialize error: {ex.Message}");
                LogToFile($"INITIALIZE_ERROR - {ex.Message}");
                return false;
            }
        }

        public bool ConnectKLine(uint baudRate = 10400)
        {
            if (_deviceId == IntPtr.Zero) return false;

            try
            {
                // Определяем правильные флаги для устройства
                uint protocol = (uint)Protocol.ISO9141;
                uint flags = GetConnectFlagsForDevice();
                string flagsDescription = GetFlagsDescription(flags);

                Console.WriteLine($"Connecting with flags: {flagsDescription}");
                LogToFile($"CONNECTING - Protocol: ISO9141, BaudRate: {baudRate}, Flags: {flagsDescription}");

                var result = _PassThruConnect(_deviceId, protocol, flags, baudRate, out _channelId);

                if (result == J2534Err.STATUS_NOERROR && _channelId != IntPtr.Zero)
                {
                    Console.WriteLine($"✓ Connected to K-Line: 0x{_channelId:X8}, {baudRate} baud");
                    LogToFile($"CONNECTED - Channel: 0x{_channelId:X8}, BaudRate: {baudRate}");

                    // Настраиваем тайминги
                    SetupTiming(20, Parity.NO_PARITY);

                    // Настраиваем фильтр
                    SetupPassAllFilter();

                    return true;
                }

                Console.WriteLine($"✗ PassThruConnect failed: {result}");
                LogToFile($"CONNECT_FAILED - Error: {result}");
                ReportError();

                // Пробуем альтернативные настройки если первая попытка не удалась
                if (result == J2534Err.ERR_INVALID_FLAGS)
                {
                    Console.WriteLine("Trying alternative connection settings...");
                    return TryAlternativeConnection(baudRate);
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Connect error: {ex.Message}");
                LogToFile($"CONNECT_ERROR - {ex.Message}");
                return false;
            }
        }

        private uint GetConnectFlagsForDevice()
        {
            // Определяем флаги в зависимости от устройства
            if (_deviceName != null)
            {
                if (_deviceName.Contains("mongoose") || _deviceName.Contains("jlr"))
                {
                    // Mongoose JLR требует специфичные флаги
                    return (uint)(MongooseFlags.DT_SNIFF_MODE | MongooseFlags.K_LINE_ONLY | MongooseFlags.CHECKSUM_DISABLED);
                }
                else if (_deviceName.Contains("op20pt32") || _deviceName.Contains("openport"))
                {
                    // OpenPort 2.0 работает с ISO9141_NO_CHECKSUM
                    return (uint)MongooseFlags.ISO9141_NO_CHECKSUM;
                }
            }

            // По умолчанию для неизвестных устройств
            return (uint)MongooseFlags.ISO9141_NO_CHECKSUM;
        }

        private string GetFlagsDescription(uint flags)
        {
            var descriptions = new List<string>();

            if ((flags & (uint)MongooseFlags.DT_SNIFF_MODE) != 0)
                descriptions.Add("DT_SNIFF_MODE");
            if ((flags & (uint)MongooseFlags.K_LINE_ONLY) != 0)
                descriptions.Add("K_LINE_ONLY");
            if ((flags & (uint)MongooseFlags.CHECKSUM_DISABLED) != 0)
                descriptions.Add("CHECKSUM_DISABLED");
            if ((flags & (uint)MongooseFlags.ISO9141_NO_CHECKSUM) != 0)
                descriptions.Add("ISO9141_NO_CHECKSUM");

            return string.Join(" | ", descriptions);
        }

        private bool TryAlternativeConnection(uint baudRate)
        {
            // Пробуем разные комбинации флагов
            var alternativeFlags = new[]
            {
                new { Flags = (uint)MongooseFlags.DT_SNIFF_MODE | (uint)MongooseFlags.K_LINE_ONLY, Description = "DT_SNIFF_MODE | K_LINE_ONLY" },
                new { Flags = (uint)MongooseFlags.DT_SNIFF_MODE | (uint)MongooseFlags.CHECKSUM_DISABLED, Description = "DT_SNIFF_MODE | CHECKSUM_DISABLED" },
                new { Flags = (uint)MongooseFlags.K_LINE_ONLY | (uint)MongooseFlags.CHECKSUM_DISABLED, Description = "K_LINE_ONLY | CHECKSUM_DISABLED" },
                new { Flags = (uint)MongooseFlags.DT_SNIFF_MODE, Description = "DT_SNIFF_MODE" },
                new { Flags = (uint)MongooseFlags.K_LINE_ONLY, Description = "K_LINE_ONLY" },
                new { Flags = (uint)MongooseFlags.CHECKSUM_DISABLED, Description = "CHECKSUM_DISABLED" },
                new { Flags = (uint)MongooseFlags.ISO9141_NO_CHECKSUM, Description = "ISO9141_NO_CHECKSUM" },
                new { Flags = 0u, Description = "NONE" }
            };

            foreach (var config in alternativeFlags)
            {
                Console.WriteLine($"Trying flags: {config.Description}");
                LogToFile($"CONNECT_ALTERNATIVE - Trying flags: {config.Description}");

                var result = _PassThruConnect(_deviceId, (uint)Protocol.ISO9141, config.Flags, baudRate, out _channelId);

                if (result == J2534Err.STATUS_NOERROR && _channelId != IntPtr.Zero)
                {
                    Console.WriteLine($"✓ Connected with flags: {config.Description}");
                    LogToFile($"CONNECTED_ALTERNATIVE - Flags: {config.Description}, Channel: 0x{_channelId:X8}");

                    SetupTiming(20, Parity.NO_PARITY);
                    SetupPassAllFilter();
                    return true;
                }

                if (result != J2534Err.ERR_INVALID_FLAGS)
                {
                    Console.WriteLine($"Failed: {result}");
                    LogToFile($"CONNECT_ALTERNATIVE_FAILED - Flags: {config.Description}, Error: {result}");
                    ReportError();
                }
            }

            Console.WriteLine("✗ All connection attempts failed");
            LogToFile("CONNECT_ALL_ATTEMPTS_FAILED");
            return false;
        }

        private void SetupTiming(uint timeout, Parity parity)
        {
            try
            {
                // Для Mongoose может не поддерживаться SET_CONFIG, поэтому игнорируем ошибки
                SCONFIG[] configs = new SCONFIG[2];
                configs[0].Parameter = (uint)ConfigParam.P1_MAX;
                configs[0].Value = timeout * 2;
                configs[1].Parameter = (uint)ConfigParam.PARITY;
                configs[1].Value = (uint)parity;

                int configSize = Marshal.SizeOf(typeof(SCONFIG));
                IntPtr configsPtr = Marshal.AllocHGlobal(configSize * configs.Length);

                for (int i = 0; i < configs.Length; i++)
                {
                    IntPtr configPtr = IntPtr.Add(configsPtr, i * configSize);
                    Marshal.StructureToPtr(configs[i], configPtr, false);
                }

                SCONFIG_LIST configList = new SCONFIG_LIST
                {
                    NumOfParams = (uint)configs.Length,
                    ConfigPtr = configsPtr
                };

                IntPtr configListPtr = Marshal.AllocHGlobal(Marshal.SizeOf(configList));
                Marshal.StructureToPtr(configList, configListPtr, false);

                var result = _PassThruIoctl(_channelId, (uint)IoctlID.SET_CONFIG, configListPtr, IntPtr.Zero);

                Marshal.FreeHGlobal(configsPtr);
                Marshal.FreeHGlobal(configListPtr);

                if (result == J2534Err.STATUS_NOERROR)
                {
                    Console.WriteLine($"✓ Timing configured: P1_MAX={timeout * 2}, PARITY={parity}");
                    LogToFile($"TIMING_CONFIGURED - P1_MAX: {timeout * 2}, PARITY: {parity}");
                }
                else
                {
                    Console.WriteLine($"⚠ Timing configuration not supported: {result}");
                    LogToFile($"TIMING_CONFIG_FAILED - Error: {result}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠ Timing setup error: {ex.Message}");
                LogToFile($"TIMING_SETUP_ERROR - {ex.Message}");
            }
        }

        private void SetupPassAllFilter()
        {
            try
            {
                // Создаем "pass all" фильтр как в C++ примере
                var maskMsg = new PassThruMsg
                {
                    ProtocolID = (uint)Protocol.ISO9141,
                    RxStatus = 0,
                    TxFlags = 0,
                    Timestamp = 0,
                    DataSize = 1,
                    ExtraDataIndex = 0
                };
                Array.Clear(maskMsg.Data, 0, maskMsg.Data.Length);

                var patternMsg = maskMsg; // Такая же структура

                // Выделяем память
                IntPtr maskMsgPtr = Marshal.AllocHGlobal(Marshal.SizeOf(maskMsg));
                IntPtr patternMsgPtr = Marshal.AllocHGlobal(Marshal.SizeOf(patternMsg));

                Marshal.StructureToPtr(maskMsg, maskMsgPtr, false);
                Marshal.StructureToPtr(patternMsg, patternMsgPtr, false);

                IntPtr filterId;
                var result = _PassThruStartMsgFilter(_channelId,
                    1, // PASS_FILTER
                    maskMsgPtr, patternMsgPtr, IntPtr.Zero, // NULL для flow control
                    out filterId);

                // Освобождаем память
                Marshal.FreeHGlobal(maskMsgPtr);
                Marshal.FreeHGlobal(patternMsgPtr);

                if (result == J2534Err.STATUS_NOERROR)
                {
                    Console.WriteLine($"✓ Pass-all filter setup: 0x{filterId:X8}");
                    LogToFile($"FILTER_SETUP - FilterID: 0x{filterId:X8}");
                }
                else
                {
                    Console.WriteLine($"⚠ Filter setup failed: {result}");
                    LogToFile($"FILTER_SETUP_FAILED - Error: {result}");
                    ReportError();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠ Filter setup error: {ex.Message}");
                LogToFile($"FILTER_SETUP_ERROR - {ex.Message}");
            }
        }

        public void StartMonitoring()
        {
            if (_channelId == IntPtr.Zero)
            {
                Console.WriteLine("Channel not connected!");
                return;
            }

            Console.WriteLine("📡 Starting K-Line monitoring (Press 'q' to stop)...");
            Console.WriteLine("Format: [Timestamp] HexData");
            LogToFile("MONITORING_STARTED");

            int messageCount = 0;
            int byteCount = 0;
            DateTime lastStatusUpdate = DateTime.Now;

            // Выделяем память для одного сообщения (как в C++ примере)
            IntPtr messagePtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(PassThruMsg)));

            try
            {
                while (!Console.KeyAvailable || Console.ReadKey(true).Key != ConsoleKey.Q)
                {
                    uint numMsgs = 1;
                    var result = _PassThruReadMsgs(_channelId, messagePtr, ref numMsgs, 1000);

                    if (result == J2534Err.STATUS_NOERROR && numMsgs > 0)
                    {
                        // Получаем сообщение
                        PassThruMsg message = Marshal.PtrToStructure<PassThruMsg>(messagePtr);

                        // Пропускаем START_OF_MESSAGE как в C++ примере
                        if ((message.RxStatus & (uint)RxStatus.START_OF_MESSAGE) == 0)
                        {
                            var logEntry = DumpMessage(message);
                            LogToFile(logEntry);
                            messageCount++;
                            byteCount += (int)message.DataSize;
                        }

                        // Обновляем статус каждую секунду
                        if ((DateTime.Now - lastStatusUpdate).TotalSeconds >= 1)
                        {
                            lastStatusUpdate = DateTime.Now;
                            Console.Write($"\rMessages: {messageCount}, Bytes: {byteCount} - Press 'q' to stop");
                        }
                    }
                    else if (result == J2534Err.ERR_BUFFER_EMPTY || result == J2534Err.ERR_TIMEOUT)
                    {
                        // Нет данных - нормально
                    }
                    else
                    {
                        Console.WriteLine($"\rRead error: {result}");
                        LogToFile($"READ_ERROR - {result}");
                        ReportError();
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(messagePtr);
                Console.WriteLine($"\n📡 Monitoring stopped. Total: {messageCount} messages, {byteCount} bytes");
                LogToFile($"MONITORING_STOPPED - Total: {messageCount} messages, {byteCount} bytes");
            }
        }

        private string DumpMessage(PassThruMsg msg)
        {
            var timestamp = msg.Timestamp;
            var data = BitConverter.ToString(msg.Data, 0, (int)msg.DataSize).Replace("-", " ");
            var direction = (msg.RxStatus & (uint)RxStatus.TX_MSG_TYPE) != 0 ? "TX" : "RX";

            var consoleOutput = $"[{timestamp}] {direction} [{data}]";
            var logOutput = $"MSG - {direction} [{data}] Size: {msg.DataSize}";

            Console.WriteLine(consoleOutput);
            return logOutput;
        }

        public bool SendMessage(byte[] data)
        {
            if (_channelId == IntPtr.Zero)
            {
                Console.WriteLine("Channel not connected!");
                return false;
            }

            try
            {
                var msg = new PassThruMsg
                {
                    ProtocolID = (uint)Protocol.ISO9141,
                    DataSize = (uint)data.Length,
                    TxFlags = 0,
                    RxStatus = 0,
                    Timestamp = 0,
                    ExtraDataIndex = 0
                };

                Array.Copy(data, msg.Data, Math.Min(data.Length, 4128));

                IntPtr msgPtr = Marshal.AllocHGlobal(Marshal.SizeOf(msg));
                Marshal.StructureToPtr(msg, msgPtr, false);

                uint numMsgs = 1;
                var result = _PassThruWriteMsgs(_channelId, msgPtr, ref numMsgs, 1000);

                Marshal.FreeHGlobal(msgPtr);

                if (result == J2534Err.STATUS_NOERROR)
                {
                    var hexData = BitConverter.ToString(data).Replace("-", " ");
                    Console.WriteLine($"✓ Message sent: {hexData}");
                    LogToFile($"SENT - [{hexData}] Size: {data.Length}");
                    return true;
                }
                else
                {
                    Console.WriteLine($"✗ SendMessage failed: {result}");
                    LogToFile($"SEND_FAILED - Error: {result}");
                    ReportError();
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Send error: {ex.Message}");
                LogToFile($"SEND_ERROR - {ex.Message}");
                return false;
            }
        }

        public bool StartPeriodicMessage(byte[] data, uint interval)
        {
            if (_channelId == IntPtr.Zero)
            {
                Console.WriteLine("Channel not connected!");
                return false;
            }

            try
            {
                // Останавливаем предыдущее периодическое сообщение если есть
                StopPeriodicMessage();

                var msg = new PassThruMsg
                {
                    ProtocolID = (uint)Protocol.ISO9141,
                    DataSize = (uint)data.Length,
                    TxFlags = 0,
                    RxStatus = 0,
                    Timestamp = 0,
                    ExtraDataIndex = 0
                };

                Array.Copy(data, msg.Data, Math.Min(data.Length, 4128));

                // Выделяем память для сообщения
                IntPtr msgPtr = Marshal.AllocHGlobal(Marshal.SizeOf(msg));
                Marshal.StructureToPtr(msg, msgPtr, false);

                var result = _PassThruStartPeriodicMsg(_channelId, msgPtr, out _periodicMsgId, interval);

                // Освобождаем память
                Marshal.FreeHGlobal(msgPtr);

                if (result == J2534Err.STATUS_NOERROR)
                {
                    var hexData = BitConverter.ToString(data).Replace("-", " ");
                    Console.WriteLine($"✓ Periodic message started: ID 0x{_periodicMsgId:X8}, interval {interval}ms");
                    Console.WriteLine($"  Message: {hexData}");
                    LogToFile($"PERIODIC_STARTED - ID: 0x{_periodicMsgId:X8}, Interval: {interval}ms, Message: [{hexData}]");
                    return true;
                }
                else
                {
                    Console.WriteLine($"✗ StartPeriodicMessage failed: {result}");
                    LogToFile($"PERIODIC_START_FAILED - Error: {result}");
                    ReportError();
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Periodic message error: {ex.Message}");
                LogToFile($"PERIODIC_ERROR - {ex.Message}");
                return false;
            }
        }

        public void StopPeriodicMessage()
        {
            if (_periodicMsgId != IntPtr.Zero && _channelId != IntPtr.Zero)
            {
                try
                {
                    var result = _PassThruStopPeriodicMsg(_channelId, _periodicMsgId);
                    if (result == J2534Err.STATUS_NOERROR)
                    {
                        Console.WriteLine($"✓ Periodic message stopped: ID 0x{_periodicMsgId:X8}");
                        LogToFile($"PERIODIC_STOPPED - ID: 0x{_periodicMsgId:X8}");
                    }
                    else
                    {
                        Console.WriteLine($"⚠ StopPeriodicMessage failed: {result}");
                        LogToFile($"PERIODIC_STOP_FAILED - ID: 0x{_periodicMsgId:X8}, Error: {result}");
                        ReportError();
                    }
                    _periodicMsgId = IntPtr.Zero;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠ StopPeriodicMessage error: {ex.Message}");
                    LogToFile($"PERIODIC_STOP_ERROR - {ex.Message}");
                }
            }
        }

        public List<string> SendAndListen(byte[] data, int listenDurationMs = 5000)
        {
            var receivedMessages = new List<string>();

            if (_channelId == IntPtr.Zero)
            {
                Console.WriteLine("Channel not connected!");
                return receivedMessages;
            }

            // Очищаем буфер приема перед отправкой
            ClearRxBuffer();

            var hexData = BitConverter.ToString(data).Replace("-", " ");
            Console.WriteLine($"\n📤 Sending: {hexData}");
            Console.WriteLine($"📡 Listening for responses for {listenDurationMs}ms...\n");
            LogToFile($"SEND_AND_LISTEN_START - Message: [{hexData}], Duration: {listenDurationMs}ms");

            // Отправляем сообщение
            if (!SendMessage(data))
            {
                return receivedMessages;
            }

            // Запускаем прослушивание ответов
            var startTime = DateTime.Now;
            IntPtr messagePtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(PassThruMsg)));

            try
            {
                while ((DateTime.Now - startTime).TotalMilliseconds < listenDurationMs)
                {
                    uint numMsgs = 1;
                    var result = _PassThruReadMsgs(_channelId, messagePtr, ref numMsgs, 100);

                    if (result == J2534Err.STATUS_NOERROR && numMsgs > 0)
                    {
                        PassThruMsg message = Marshal.PtrToStructure<PassThruMsg>(messagePtr);

                        if ((message.RxStatus & (uint)RxStatus.START_OF_MESSAGE) == 0 && message.DataSize > 0)
                        {
                            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                            var messageData = BitConverter.ToString(message.Data, 0, (int)message.DataSize).Replace("-", " ");
                            var direction = (message.RxStatus & (uint)RxStatus.TX_MSG_TYPE) != 0 ? "TX" : "RX";

                            var logEntry = $"{timestamp} {direction} [{messageData}]";
                            var fileLogEntry = $"SEND_AND_LISTEN_RESPONSE - {direction} [{messageData}] Size: {message.DataSize}";

                            Console.WriteLine(logEntry);
                            LogToFile(fileLogEntry);
                            receivedMessages.Add(logEntry);
                        }
                    }

                    // Проверяем не нажата ли клавиша для досрочного выхода
                    if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q)
                    {
                        Console.WriteLine("\n⏹️  Listening stopped by user");
                        LogToFile("SEND_AND_LISTEN_STOPPED_BY_USER");
                        break;
                    }

                    Thread.Sleep(10);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(messagePtr);
            }

            Console.WriteLine($"\n📋 Received {receivedMessages.Count} message(s)");
            LogToFile($"SEND_AND_LISTEN_COMPLETED - Received: {receivedMessages.Count} messages");
            return receivedMessages;
        }

        private void ClearRxBuffer()
        {
            try
            {
                // Очищаем буфер приема с помощью IOCTL
                var result = _PassThruIoctl(_channelId, (uint)IoctlID.CLEAR_RX_BUFFER, IntPtr.Zero, IntPtr.Zero);
                if (result == J2534Err.STATUS_NOERROR)
                {
                    LogToFile("BUFFER_CLEARED - RX buffer cleared");
                }
                else
                {
                    LogToFile($"BUFFER_CLEAR_FAILED - Error: {result}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠ Clear buffer error: {ex.Message}");
                LogToFile($"BUFFER_CLEAR_ERROR - {ex.Message}");
            }
        }

        public void PrintVersionInfo()
        {
            if (_deviceId == IntPtr.Zero) return;

            try
            {
                var apiVersion = new StringBuilder(256);
                var firmwareVersion = new StringBuilder(256);
                var dllVersion = new StringBuilder(256);

                var result = _PassThruReadVersion(_deviceId, apiVersion, firmwareVersion, dllVersion);

                if (result == J2534Err.STATUS_NOERROR)
                {
                    Console.WriteLine($"J2534 API Version: {apiVersion}");
                    Console.WriteLine($"J2534 DLL Version: {dllVersion}");
                    Console.WriteLine($"Device Firmware Version: {firmwareVersion}");

                    LogToFile($"VERSION_INFO - API: {apiVersion}, DLL: {dllVersion}, Firmware: {firmwareVersion}");
                }
                else
                {
                    Console.WriteLine($"✗ PassThruReadVersion failed: {result}");
                    LogToFile($"VERSION_INFO_FAILED - Error: {result}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Version info error: {ex.Message}");
                LogToFile($"VERSION_INFO_ERROR - {ex.Message}");
            }
        }

        private void ReportError()
        {
            try
            {
                var errorDesc = new StringBuilder(256);
                var result = _PassThruGetLastError(errorDesc);
                if (result == J2534Err.STATUS_NOERROR)
                {
                    Console.WriteLine($"J2534 Error: {errorDesc}");
                    LogToFile($"J2534_ERROR - {errorDesc}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reporting failed: {ex.Message}");
                LogToFile($"ERROR_REPORTING_FAILED - {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // Останавливаем периодическое сообщение при закрытии
                StopPeriodicMessage();

                if (_channelId != IntPtr.Zero)
                {
                    _PassThruDisconnect(_channelId);
                    _channelId = IntPtr.Zero;
                    LogToFile("CHANNEL_DISCONNECTED");
                }

                if (_deviceId != IntPtr.Zero)
                {
                    _PassThruClose(_deviceId);
                    _deviceId = IntPtr.Zero;
                    LogToFile("DEVICE_CLOSED");
                }

                if (_libraryHandle != IntPtr.Zero)
                {
                    NativeMethods.FreeLibrary(_libraryHandle);
                    _libraryHandle = IntPtr.Zero;
                }

                // Закрываем лог файл
                if (_logWriter != null)
                {
                    LogToFile($"=== K-Line Session Ended: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                    _logWriter.Close();
                    _logWriter.Dispose();
                    _logWriter = null;
                    Console.WriteLine($"📝 Log file saved: {_logFilePath}");
                }

                _disposed = true;
            }
        }
    }
}