using System;
using System.IO.Ports;
using System.Linq;
using System.Threading;

namespace ControlGemini88
{
    internal class Program
    {
        static void Main(string[] args)
        {
            SerialPort puertoSerial = new SerialPort();
            string[] puertosDisponibles = SerialPort.GetPortNames();

            // Verificar si hay puertos COM disponibles
            if (puertosDisponibles.Length == 0)
            {
                Console.WriteLine("No se encontraron puertos COM disponibles.");
                return;
            }

            // Intentar abrir el primer puerto COM disponible
            foreach (string puerto in puertosDisponibles)
            {
                try
                {
                    // Configurar el puerto serial
                    puertoSerial.PortName = puerto;
                    puertoSerial.BaudRate = 300;            // Verificar que coincide con la máquina
                    puertoSerial.Parity = Parity.None;
                    puertoSerial.StopBits = StopBits.Two;
                    puertoSerial.Handshake = Handshake.None;
                    puertoSerial.ReadTimeout = 4000;
                    puertoSerial.WriteTimeout = 4000;

                    // Intentar abrir el puerto
                    puertoSerial.Open();
                    Console.WriteLine("El puerto " + puerto + " se ha abierto exitosamente.");
                    break; // Salir del bucle si se abre correctamente
                }
                catch (Exception ex)
                {
                    // Mostrar mensaje si no se puede abrir el puerto
                    Console.WriteLine($"No se pudo abrir el puerto {puerto}: {ex.Message}");
                }
            }

            // Verificar si se logró abrir algún puerto COM
            if (!puertoSerial.IsOpen)
            {
                Console.WriteLine("No se pudo abrir ningún puerto COM disponible.");
                return;
            }

            try
            {
                // Selección de jeringa (A o B)
                char seleccion = ObtenerSeleccionJeringaDelUsuario();

                // Obtener parámetros de usuario según la selección de jeringa
                double ratio = 0.0;
                double diametro = 0.0;
                double volumen = 0.0;

                if (seleccion == 'A')
                {
                    ratio = ObtenerDoubleDelUsuario("Ingrese el ratio de titulación para la jeringa A (ml/min): ");
                    diametro = ObtenerDoubleDelUsuario("Ingrese el diámetro de la jeringa A (mm): ");
                    volumen = ObtenerDoubleDelUsuario("Ingrese el volumen de titulación para la jeringa A (ml): ");
                }
                else if (seleccion == 'B')
                {
                    ratio = ObtenerDoubleDelUsuario("Ingrese el ratio de titulación para la jeringa B (ml/min): ");
                    diametro = ObtenerDoubleDelUsuario("Ingrese el diámetro de la jeringa B (mm): ");
                    volumen = ObtenerDoubleDelUsuario("Ingrese el volumen de titulación para la jeringa B (ml): ");
                }
                else
                {
                    Console.WriteLine("Selección no válida.");
                    return;
                }

                string direccion = ObtenerDireccionDelUsuario("Ingrese la dirección de bombeo (INF para infusión, WDR para extracción): ");

                int numInstruc = 7;           // número de instrucciones

                string[] paramBombeo = new string[numInstruc];
                string finEnvio = "\r\n";

                // Configurar parámetros de bombeo según la selección de jeringa
                paramBombeo[0] = "MOD PRO ";                              // modo de operación
                paramBombeo[1] = "PAR ON ";                               // Parallel or reciprocal  
                paramBombeo[2] = "DIA " + seleccion + " " + diametro.ToString();   // diametro de la jeringa seleccionada
                paramBombeo[3] = "DIR " + direccion;                       // dirección de bombeo
                paramBombeo[4] = "RAT " + seleccion + " " + ratio.ToString() + " MM"; // ratio de bombeo de la jeringa seleccionada
                paramBombeo[5] = "RAT " + (seleccion == 'A' ? 'B' : 'A') + " 0.0 MM"; // ratio de bombeo de la otra jeringa (0.0 para desactivar)
                paramBombeo[6] = "PAR OFF";                                // Desactivar modo paralelo

                // Enviar los comandos de bombeo al dispositivo
                for (int i = 0; i < numInstruc; i++)
                {
                    puertoSerial.Write(paramBombeo[i] + finEnvio);
                    Console.WriteLine("Dato enviado {0}", paramBombeo[i]);
                }

                // Recepción de agradecimientos a los parámetros de bombeo
                int espera = 3;
                Thread.Sleep(espera * 1000);
                string msgRecibido = puertoSerial.ReadExisting();
                if (msgRecibido == "")
                {
                    Console.WriteLine("Error en la conexión con el equipo kdScientific");
                    Console.WriteLine("Confirmar: \n1/ El address del equipo tiene que ser 00\n2/ El ratio de baudios de la RS232 Gearmo tiene que ser 300");
                    Console.ReadKey();
                    System.Environment.Exit(0);
                }
                msgRecibido = msgRecibido.Trim('\n');
                string[] respuestas = msgRecibido.Split('\n');

                Console.WriteLine("Hasta aquí funciona bien");

                // Impresión de instrucciones y agradecimientos a los parámetros de bombeo
                Console.WriteLine("Instrucciones y agradecimientos en el puerto {0} después de {1} segundos",
                                    puertoSerial.PortName, espera);
                for (int i = 0; i < numInstruc; i++)
                {
                    Console.WriteLine("Instrucción:\t{0} \t\tRespuesta:\t{1}", paramBombeo[i], respuestas[i]);
                }

                // Comprobación de los parámetros de bombeo
                bool flag = true;
                for (int i = 0; i < numInstruc; i++)
                {
                    if (!respuestas[i].StartsWith("0:"))
                    {
                        flag = false;
                    }
                }

                // Ejecución del bombeo si los parámetros son correctos
                if (flag)
                {
                    Console.WriteLine("\nSe inyectan " + volumen + " ml con los parámetros actuales");
                    int tiempo = (int)(60 * volumen / ratio); // tiempo de titulación en segundos
                    Console.WriteLine("Se necesitan " + tiempo + " segundos para el bombeo");
                    string run = "RUN ";
                    puertoSerial.Write(run + finEnvio);
                    Thread.Sleep(tiempo * 1000);
                    string stop = "STP ";
                    puertoSerial.Write(stop + finEnvio);
                }
                else
                {
                    Console.WriteLine("\n\nERROR: Algún parámetro de bombeo es incorrecto");
                }

                puertoSerial.Close(); // Cerrar el puerto serial al finalizar
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            // Esperar a que el usuario presione Enter para salir
            Console.WriteLine("\nPresione Enter para salir...");
            Console.ReadLine();
        }

        // Método para obtener la selección de jeringa (A o B) del usuario
        static char ObtenerSeleccionJeringaDelUsuario()
        {
            Console.Write("Seleccione la jeringa (A o B): ");
            while (true)
            {
                string input = Console.ReadLine().ToUpper();
                if (input == "A" || input == "B")
                {
                    return input[0];
                }
                Console.Write("Selección no válida. Seleccione la jeringa (A o B): ");
            }
        }

        // Método para obtener un valor double del usuario
        static double ObtenerDoubleDelUsuario(string mensaje)
        {
            Console.Write(mensaje);
            while (true)
            {
                if (double.TryParse(Console.ReadLine(), out double valor))
                {
                    return valor;
                }
                Console.Write("Entrada no válida. " + mensaje);
            }
        }

        // Método para obtener la dirección de bombeo (INF o WDR) del usuario
        static string ObtenerDireccionDelUsuario(string mensaje)
        {
            Console.Write(mensaje);
            while (true)
            {
                string input = Console.ReadLine().ToUpper();
                if (input == "INF" || input == "WDR")
                {
                    return input;
                }
                Console.Write("Entrada no válida. " + mensaje);
            }
        }
    }
}

