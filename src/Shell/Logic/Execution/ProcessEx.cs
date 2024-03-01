using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Dotnet.Shell.Logic.Execution
{
    public class ProcessEx : IDisposable
    {
        private delegate object StringParseFunc(string strRep);
        private delegate object BinaryParseFunc(Stream stream);

        private static readonly Dictionary<Type, Tuple<StringParseFunc, BinaryParseFunc>> CmdToConversionLookupTable = new()
        {
            // basic types
            [typeof(bool)] = new Tuple<StringParseFunc, BinaryParseFunc>((str) => bool.Parse(str), (stream) => stream.ReadByte() != 0x0),
            [typeof(byte)] = new Tuple<StringParseFunc, BinaryParseFunc>((str) => byte.Parse(str), (stream) => stream.ReadByte()),
            [typeof(sbyte)] = new Tuple<StringParseFunc, BinaryParseFunc>((str) => sbyte.Parse(str), (stream) => (sbyte)stream.ReadByte()),
            [typeof(char)] = new Tuple<StringParseFunc, BinaryParseFunc>((str) => char.Parse(str), (stream) => (char)stream.ReadByte()),
            [typeof(decimal)] = new Tuple<StringParseFunc, BinaryParseFunc>((str) => decimal.Parse(str), (stream) => throw new InvalidDataException("Could not cast result to decimal")),
            [typeof(double)] = new Tuple<StringParseFunc, BinaryParseFunc>((str) => double.Parse(str), (stream) => { var b = new byte[sizeof(double)]; stream.Read(b, 0, b.Length); return BitConverter.ToDouble(b, 0); }),
            [typeof(float)] = new Tuple<StringParseFunc, BinaryParseFunc>((str) => float.Parse(str), (stream) => throw new InvalidDataException("Could not cast result to float")),
            [typeof(int)] = new Tuple<StringParseFunc, BinaryParseFunc>((str) => int.Parse(str), (stream) => { var b = new byte[sizeof(int)]; stream.Read(b, 0, b.Length); return BitConverter.ToInt32(b, 0); }),
            [typeof(uint)] = new Tuple<StringParseFunc, BinaryParseFunc>((str) => uint.Parse(str), (stream) => { var b = new byte[sizeof(uint)]; stream.Read(b, 0, b.Length); return BitConverter.ToUInt32(b, 0); }),
            [typeof(long)] = new Tuple<StringParseFunc, BinaryParseFunc>((str) => long.Parse(str), (stream) => { var b = new byte[sizeof(long)]; stream.Read(b, 0, b.Length); return BitConverter.ToInt64(b, 0); }),
            [typeof(ulong)] = new Tuple<StringParseFunc, BinaryParseFunc>((str) => ulong.Parse(str), (stream) => { var b = new byte[sizeof(ulong)]; stream.Read(b, 0, b.Length); return BitConverter.ToUInt64(b, 0); }),
            [typeof(short)] = new Tuple<StringParseFunc, BinaryParseFunc>((str) => short.Parse(str), (stream) => { var b = new byte[sizeof(short)]; stream.Read(b, 0, b.Length); return BitConverter.ToInt16(b, 0); }),
            [typeof(ushort)] = new Tuple<StringParseFunc, BinaryParseFunc>((str) => ushort.Parse(str), (stream) => { var b = new byte[sizeof(ushort)]; stream.Read(b, 0, b.Length); return BitConverter.ToUInt16(b, 0); }),
            [typeof(string)] = new Tuple<StringParseFunc, BinaryParseFunc>((str) => str, (stream) => throw new InvalidProgramException()),
            [typeof(object)] = new Tuple<StringParseFunc, BinaryParseFunc>((str) => str, (stream) => throw new InvalidProgramException()),

            // more complex types
            [typeof(string[])] = new Tuple<StringParseFunc, BinaryParseFunc>((str) => str.Split(Environment.NewLine), (stream) => throw new InvalidProgramException()),
            [typeof(List<string>)] = new Tuple<StringParseFunc, BinaryParseFunc>((str) => { var ret = new List<string>(); ret.AddRange(str.Split(Environment.NewLine)); return ret; }, (stream) => throw new InvalidProgramException() ),
            [typeof(Stream)] = new Tuple<StringParseFunc, BinaryParseFunc>((str) => throw new InvalidProgramException(), (stream) => { var x = new MemoryStream(); stream.CopyTo(x); x.Position = 0; return x; } ),
            [typeof(FileInfo)] = new Tuple<StringParseFunc, BinaryParseFunc>((str) => { str = str.Trim(); if (File.Exists(str)) { return new FileInfo(str); } else { throw new Exception(); } }, (stream) => throw new InvalidDataException("Could not cast result to file path")),
            [typeof(DirectoryInfo)] = new Tuple<StringParseFunc, BinaryParseFunc>((str) => { str = str.Trim(); if (Directory.Exists(str)) { return new DirectoryInfo(str); } else { throw new Exception(); } }, (stream) => throw new InvalidDataException("Could not cast result to directory path")),
        };

        private bool disposedValue;
        private readonly Process p;
        private readonly CancellationTokenSource terminateProcessTokenSource = new();
        private readonly MemoryStream InternalStdOut = new MemoryStream();
        private readonly MemoryStream InternalStdErr = new MemoryStream();
        private CancellationTokenSource suspendProcessTokenSource = new();
        private bool suspended = false;

        public Process Process => p;

        public ProcessEx(Process p)
        {
            this.p = p;
        }

        public void SignalTerminate()
        {
            terminateProcessTokenSource.Cancel();
        }

        public void SignalSuspend()
        {
            suspendProcessTokenSource.Cancel();
        }

        /// <summary>
        /// Sets the last exit code.
        /// </summary>
        /// <param name="process">The process.</param>
        /// <param name="s">The s.</param>
        /// <returns></returns>
        public ProcessEx WaitTillExit(object s)
        {
            var shell = s as Dotnet.Shell.API.Shell;

            shell.SetForegroundProcess(this);

            if (suspended)
            {
                shell.RemoveFromBackgroundProcesses(this);
                Resume();
            }

            // I've noticed an issue on net6 (windows) and now .net8 where if the process
            // has 'lots' of stdout then WaitForExitAsync will never return false. To work around
            // this if we have opted for redirection we copy to an internal stream
            if (p.StartInfo.RedirectStandardOutput)
            {
                p.StandardOutput.BaseStream.CopyTo(InternalStdOut);
                InternalStdOut.Position = 0;
            }

            if (p.StartInfo.RedirectStandardError)
            {
                p.StandardError.BaseStream.CopyTo(InternalStdErr);
                InternalStdErr.Position = 0;
            }

            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(terminateProcessTokenSource.Token, suspendProcessTokenSource.Token))
            {
                try
                {
                    Task task = p.WaitForExitAsync(cts.Token);
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
                    task.Wait(cts.Token);
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits

                    if (task.IsCompletedSuccessfully)
                    {
                        shell.LastExitCode = p.ExitCode;
                    }
                }
                catch
                {
                }
            }

            p.Refresh();
            if (terminateProcessTokenSource.IsCancellationRequested && !p.HasExited)
            {
                p.Kill();
            }
            else if (suspendProcessTokenSource.IsCancellationRequested && !p.HasExited)
            {
                Suspend();
                shell.AddToBackgroundProcesses(this);

                // we need to create a new suspendProcessTokenSource for when we are reactivated
                suspendProcessTokenSource.Dispose();
                suspendProcessTokenSource = new CancellationTokenSource();
            }

            shell.SetForegroundProcess(null);

            return this;
        }

        internal void Suspend()
        {
            // todo is this naive, what about children? Is it ok as everything is wrapped in bash?

            p.Refresh();
            if (!p.HasExited)
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    OS.Exec("kill -STOP " + p.Id);
                }
                else
                {
                    throw new NotImplementedException("Suspend() has not yet been implemented on Windows");
                }
            }
            suspended = true;
        }

        internal void Resume()
        {
            // todo replace with library call, this is lame

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                OS.Exec("kill -CONT " + p.Id);
            }

            suspended = false;
        }

        public Task WaitForExitAsync(CancellationToken token = default)
        {
            return p.WaitForExitAsync(token);
        }

        /// <summary>
        /// Converts the standard out to variable.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="process">The process.</param>
        /// <returns></returns>
        public T ConvertStdOutToVariable<T>()
        {
            p.WaitForExit();
            return ConvertStreamToVariable<T>(InternalStdOut);
        }

        /// <summary>
        /// Converts the standard error to variable.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="process">The process.</param>
        /// <returns></returns>
        public T ConvertStdErrToVariable<T>()
        {
            p.WaitForExit();
            return ConvertStreamToVariable<T>(InternalStdErr);
        }

        /// <summary>
        /// Copies the standard error.
        /// </summary>
        /// <param name="process">The process.</param>
        /// <param name="s">The s.</param>
        /// <returns></returns>
        public ProcessEx CopyStdErr(object s, Stream stream)
        {
            if (p.StartInfo.RedirectStandardError)
            {
                p.WaitForExit();
                var stdErrTask = InternalStdErr.CopyToAsync(stream);

                try
                {
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
                    stdErrTask.Wait();
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
                }
                catch
                {

                }
            }

            return this;
        }

        private static T ConvertStreamToVariable<T>(Stream stream)
        {
            T ret;
            var typeToConvert = typeof(T);

            if (CmdToConversionLookupTable.ContainsKey(typeToConvert))
            {
                var functions = CmdToConversionLookupTable[typeToConvert];
                ret = (T)ParseRaw(functions.Item1, functions.Item2, stream);
            }
            else
            {
                // get the type and try to invoke a constructor which either takes a stream, string or a byte[]
                // for now we throw
                throw new NotImplementedException("Unable to convert to type: " + typeToConvert.FullName);
            }

            return ret;
        }

        private static object ParseRaw(StringParseFunc stringParseFunc, BinaryParseFunc binaryParseFunc, Stream stream)
        {
            object ret;

            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                memoryStream.Position = 0;

                try
                {
                    ret = stringParseFunc(StreamToString(memoryStream));
                    
                }
                catch
                {
                    memoryStream.Position = 0;
                    ret = binaryParseFunc(memoryStream);
                }
            }

            stream.Dispose();

            return ret;
        }

        private static string StreamToString(Stream stream)
        {
            using (var sr = new StreamReader(stream, leaveOpen : true))
            {
                return sr.ReadToEnd();
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    terminateProcessTokenSource.Cancel();
                    terminateProcessTokenSource.Dispose();

                    suspendProcessTokenSource.Cancel();
                    suspendProcessTokenSource.Dispose();

                    InternalStdOut.Dispose();
                    InternalStdErr.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
