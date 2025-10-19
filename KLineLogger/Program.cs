using Microsoft.Win32;

namespace KLineLogger
{
    class Program
    {
        private static J2534Wrapper? _wrapper;
        private static bool _isRunning = true;

        static void Main(string[] args)
        {
            Console.WriteLine("K-Line Monitor & Diagnostic Tool with Logging");
            Console.WriteLine("============================================\n");

            try
            {
                while (_isRunning)
                {
                    ShowMainMenu();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error: {ex.Message}");
            }
            finally
            {
                _wrapper?.Dispose();
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static void ShowMainMenu()
        {
            Console.Clear();
            Console.WriteLine("=== MAIN MENU ===");
            Console.WriteLine("1. Select Adapter and Connect");
            Console.WriteLine("2. Monitor K-Line Traffic");
            Console.WriteLine("3. Send Single Message");
            Console.WriteLine("4. Send and Listen for Response");
            Console.WriteLine("5. Start Periodic Test Message");
            Console.WriteLine("6. Stop Periodic Message");
            Console.WriteLine("7. Custom Message Tools");
            Console.WriteLine("8. Show Log File Location");
            Console.WriteLine("9. Exit");
            Console.Write("\nSelect option: ");

            var input = Console.ReadLine();
            switch (input)
            {
                case "1":
                    SelectAndConnectAdapter();
                    break;
                case "2":
                    MonitorKLine();
                    break;
                case "3":
                    SendSingleMessage();
                    break;
                case "4":
                    SendAndListen();
                    break;
                case "5":
                    StartPeriodicTestMessage();
                    break;
                case "6":
                    StopPeriodicMessage();
                    break;
                case "7":
                    ShowCustomMessageMenu();
                    break;
                case "8":
                    ShowLogLocation();
                    break;
                case "9":
                    _isRunning = false;
                    break;
                default:
                    Console.WriteLine("Invalid option!");
                    Thread.Sleep(1000);
                    break;
            }
        }

        static void SelectAndConnectAdapter()
        {
            var devices = GetJ2534Devices();

            if (devices.Count == 0)
            {
                Console.WriteLine("No J2534 devices found!");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("\nAvailable adapters:");
            for (int i = 0; i < devices.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {devices[i].Name}");
                Console.WriteLine($"   Library: {Path.GetFileName(devices[i].FunctionLibrary)}");
            }

            Console.Write($"\nSelect adapter (1-{devices.Count}): ");
            if (int.TryParse(Console.ReadLine(), out int choice) && choice >= 1 && choice <= devices.Count)
            {
                var selectedDevice = devices[choice - 1];

                _wrapper?.Dispose();
                _wrapper = new J2534Wrapper(selectedDevice.FunctionLibrary);

                Console.WriteLine($"\nInitializing {selectedDevice.Name}...");

                if (_wrapper.Initialize())
                {
                    Console.WriteLine("✓ Device initialized");
                    _wrapper.PrintVersionInfo();

                    Console.WriteLine("\nConnecting to K-Line...");
                    if (_wrapper.ConnectKLine(10400))
                    {
                        Console.WriteLine("✓ Connected to K-Line");
                    }
                    else
                    {
                        Console.WriteLine("✗ Failed to connect to K-Line");
                        _wrapper = null;
                    }
                }
                else
                {
                    Console.WriteLine("✗ Failed to initialize adapter!");
                    _wrapper = null;
                }
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        static void MonitorKLine()
        {
            if (_wrapper == null)
            {
                Console.WriteLine("No adapter selected or connected!");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("\n=== K-LINE MONITORING ===");
            Console.WriteLine("Starting monitoring... Press 'q' to stop\n");

            _wrapper.StartMonitoring();

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        static void SendSingleMessage()
        {
            if (_wrapper == null)
            {
                Console.WriteLine("No adapter selected!");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("\n=== SEND SINGLE MESSAGE ===");
            Console.WriteLine("1. Send test message: C1 33 F1 81");
            Console.WriteLine("2. Send custom message");
            Console.Write("\nSelect option: ");

            var input = Console.ReadLine();
            switch (input)
            {
                case "1":
                    SendTestMessage();
                    break;
                case "2":
                    SendCustomMessage();
                    break;
                default:
                    Console.WriteLine("Invalid option!");
                    break;
            }
        }

        static void SendTestMessage()
        {
            // Тестовое сообщение: C1 33 F1 81
            var testMessage = new byte[] { 0xC1, 0x33, 0xF1, 0x81 };

            Console.WriteLine($"Sending test message: {BitConverter.ToString(testMessage).Replace("-", " ")}");

            if (_wrapper!.SendMessage(testMessage))
            {
                Console.WriteLine("✓ Test message sent successfully");
            }
            else
            {
                Console.WriteLine("✗ Failed to send test message!");
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        static void SendCustomMessage()
        {
            Console.Write("\nEnter message in hex (e.g., C1 33 F1 81): ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                Console.WriteLine("No message entered!");
                return;
            }

            try
            {
                // Преобразуем hex строку в byte[]
                var hexBytes = input.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                var message = new byte[hexBytes.Length];

                for (int i = 0; i < hexBytes.Length; i++)
                {
                    message[i] = Convert.ToByte(hexBytes[i], 16);
                }

                Console.WriteLine($"Sending: {BitConverter.ToString(message).Replace("-", " ")}");

                if (_wrapper!.SendMessage(message))
                {
                    Console.WriteLine("✓ Message sent successfully");
                }
                else
                {
                    Console.WriteLine("✗ Failed to send message!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error: {ex.Message}");
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        static void SendAndListen()
        {
            if (_wrapper == null)
            {
                Console.WriteLine("No adapter selected!");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("\n=== SEND AND LISTEN ===");
            Console.WriteLine("1. Send test message and listen (C1 33 F1 81)");
            Console.WriteLine("2. Send custom message and listen");
            Console.Write("\nSelect option: ");

            var input = Console.ReadLine();
            byte[] message;

            switch (input)
            {
                case "1":
                    message = new byte[] { 0xC1, 0x33, 0xF1, 0x81 };
                    break;
                case "2":
                    message = GetCustomMessage();
                    if (message == null || message.Length == 0)
                    {
                        Console.WriteLine("No valid message entered!");
                        Console.ReadKey();
                        return;
                    }
                    break;
                default:
                    Console.WriteLine("Invalid option!");
                    return;
            }

            Console.Write("\nEnter listen duration in milliseconds (default 5000): ");
            if (!int.TryParse(Console.ReadLine(), out int duration) || duration <= 0)
            {
                duration = 5000;
            }

            // Отправляем и слушаем ответы
            var responses = _wrapper.SendAndListen(message, duration);

            if (responses.Count == 0)
            {
                Console.WriteLine("❌ No responses received within the specified time");
            }
            else
            {
                Console.WriteLine($"✅ Received {responses.Count} response(s)");
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        static byte[] GetCustomMessage()
        {
            Console.Write("\nEnter message in hex (e.g., C1 33 F1 81): ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                return Array.Empty<byte>();
            }

            try
            {
                var hexBytes = input.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                var message = new byte[hexBytes.Length];

                for (int i = 0; i < hexBytes.Length; i++)
                {
                    message[i] = Convert.ToByte(hexBytes[i], 16);
                }
                return message;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error parsing message: {ex.Message}");
                return Array.Empty<byte>();
            }
        }

        static void StartPeriodicTestMessage()
        {
            if (_wrapper == null)
            {
                Console.WriteLine("No adapter selected!");
                Console.ReadKey();
                return;
            }

            // Тестовое сообщение: C1 33 F1 81 с периодичностью 300 мс
            var testMessage = new byte[] { 0xC1, 0x33, 0xF1, 0x81 };

            Console.WriteLine($"Starting periodic test message: {BitConverter.ToString(testMessage).Replace("-", " ")} every 300ms");
            Console.WriteLine("Press ANY KEY to stop the periodic message");

            if (_wrapper.StartPeriodicMessage(testMessage, 300))
            {
                // Ждем нажатия клавиши для остановки
                Console.ReadKey(true);

                _wrapper.StopPeriodicMessage();
                Console.WriteLine("✓ Periodic message stopped");
            }
            else
            {
                Console.WriteLine("✗ Failed to start periodic message!");
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        static void StopPeriodicMessage()
        {
            if (_wrapper == null)
            {
                Console.WriteLine("No adapter selected!");
                Console.ReadKey();
                return;
            }

            _wrapper.StopPeriodicMessage();
            Console.WriteLine("✓ Periodic message stopped (if any was running)");

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        static void ShowCustomMessageMenu()
        {
            Console.WriteLine("\n=== CUSTOM MESSAGE TOOLS ===");
            Console.WriteLine("1. Send KWP2000 Start Communication (81 11 F1)");
            Console.WriteLine("2. Send KWP2000 Read Data (22 F1 90)");
            Console.WriteLine("3. Send KWP2000 Security Access (27 01)");
            Console.WriteLine("4. Back to main menu");
            Console.Write("\nSelect option: ");

            var input = Console.ReadLine();
            byte[] message;

            switch (input)
            {
                case "1":
                    message = new byte[] { 0x81, 0x11, 0xF1 }; // Start Communication
                    break;
                case "2":
                    message = new byte[] { 0x22, 0xF1, 0x90 }; // Read Data by Identifier
                    break;
                case "3":
                    message = new byte[] { 0x27, 0x01 }; // Security Access - Request Seed
                    break;
                case "4":
                    return;
                default:
                    Console.WriteLine("Invalid option!");
                    return;
            }

            Console.WriteLine($"Sending: {BitConverter.ToString(message).Replace("-", " ")}");

            if (_wrapper!.SendMessage(message))
            {
                Console.WriteLine("✓ Message sent successfully");
            }
            else
            {
                Console.WriteLine("✗ Failed to send message!");
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        static void ShowLogLocation()
        {
            if (_wrapper == null)
            {
                Console.WriteLine("No adapter initialized - no log file created yet.");
            }
            else
            {
                Console.WriteLine("\n📝 Logging is automatically enabled.");
                Console.WriteLine("All activities are logged to file in the 'Logs' folder.");
                Console.WriteLine("Log file is created when you select an adapter.");
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        static List<J2534Device> GetJ2534Devices()
        {
            var devices = new List<J2534Device>();

            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\PassThruSupport.04.04");
            if (key == null) return devices;

            foreach (var subKeyName in key.GetSubKeyNames())
            {
                using var subKey = key.OpenSubKey(subKeyName);
                if (subKey != null)
                {
                    var name = subKey.GetValue("Name") as string;
                    var functionLibrary = subKey.GetValue("FunctionLibrary") as string;

                    if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(functionLibrary))
                    {
                        devices.Add(new J2534Device { Name = name, FunctionLibrary = functionLibrary });
                    }
                }
            }

            return devices;
        }
    }

    public class J2534Device
    {
        public string Name { get; set; } = string.Empty;
        public string FunctionLibrary { get; set; } = string.Empty;
    }
}