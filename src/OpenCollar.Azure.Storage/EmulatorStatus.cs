/*
 * This file is part of OpenCollar.Azure.ReliableQueue.
 *
 * OpenCollar.Azure.ReliableQueue is free software: you can redistribute it
 * and/or modify it under the terms of the GNU General Public License as published
 * by the Free Software Foundation, either version 3 of the License, or (at your
 * option) any later version.
 *
 * OpenCollar.Azure.ReliableQueue is distributed in the hope that it will be
 * useful, but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public
 * License for more details.
 *
 * You should have received a copy of the GNU General Public License along with
 * OpenCollar.Azure.ReliableQueue.  If not, see <https://www.gnu.org/licenses/>.
 *
 * Copyright © 2020 Jonathan Evans (jevans@open-collar.org.uk).
 */

using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenCollar.Azure.Storage
{
    /// <summary>Executes the storage emulator and returns the represents the resulting status.</summary>
    public class EmulatorStatus
    {
        /// <summary>The regular expression for reading the version number from the first line of the status output.</summary>
        private static readonly Regex _versionRegex = new Regex("^Windows Azure Storage Emulator ([0-9]+\\.[0-9]+\\.[0-9]+\\.[0-9]+) command line tool$",
            RegexOptions.Compiled | RegexOptions.Singleline);

        /// <summary>The regular expression for reading the running state from the second line of the status output.</summary>
        private static readonly Regex _isRunningRegex = new Regex("^IsRunning: ([A-Z][a-z]+)$", RegexOptions.Compiled | RegexOptions.Singleline);

        /// <summary>The regular expression for reading the blob storage endpoint URL from the second line of the status output.</summary>
        private static readonly Regex _blobEndpointRegex = new Regex("^BlobEndpoint: (.+)$", RegexOptions.Compiled | RegexOptions.Singleline);

        /// <summary>The regular expression for reading the queue endpoint URL from the fourth line of the status output.</summary>
        private static readonly Regex _queueEndpointRegex = new Regex("^QueueEndpoint: (.+)$", RegexOptions.Compiled | RegexOptions.Singleline);

        /// <summary>The regular expression for reading the table endpoint URL from the fifth line of the status output.</summary>
        private static readonly Regex _tableEndpointRegex = new Regex("^TableEndpoint: (.+)$", RegexOptions.Compiled | RegexOptions.Singleline);

        /// <summary>Executes the Azure Storage Emulator and returns the resulting status.</summary>
        /// <param name="emulator">The emulator environment providing settings necessary to run.</param>
        /// <param name="action">The action to perform.</param>
        internal EmulatorStatus(Emulator emulator, EmulatorAction action)
        {
            string arguments;
            switch(action)
            {
                case EmulatorAction.Unknown:
                    throw new ArgumentException(@"'action' contained 'Unknown'.", nameof(action));

                case EmulatorAction.Status:
                    arguments = "status";
                    break;

                case EmulatorAction.Start:
                    arguments = "start";
                    break;

                case EmulatorAction.Stop:
                    arguments = "stop";
                    break;

                case EmulatorAction.Clear:
                    arguments = "clear";
                    break;

                case EmulatorAction.Init:
                    arguments = "init";
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, @$"'action' did not contain a recognized value: {action}.");
            }

            Action = action;

            if(!emulator.IsEmulatorExePresent)
            {
                IsInstalled = false;
                IsSuccessful = false;
                Error = @$"Azure Storage Emulator was not found to be installed at the expected location: ""{emulator.EmulatorExePath}"".";
                return;
            }

            // For actions that do not capture these details, we can, where possible, reuse those from a previous run.
            Version = emulator.Version;
            BlobEndpoint = emulator.BlobEndpoint;
            QueueEndpoint = emulator.QueueEndpoint;
            TableEndpoint = emulator.TableEndpoint;

            IsInstalled = true;

            var process = new Process
            {
                StartInfo = new ProcessStartInfo(emulator.EmulatorExePath, arguments)
                {
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WindowStyle = ProcessWindowStyle.Minimized,
                    WorkingDirectory = emulator.EmulatorDirectoryPath,
                    UseShellExecute = false
                }
            };

            var outputStringBuilder = new StringBuilder();

            process.OutputDataReceived += (sender, eventArgs) => outputStringBuilder.AppendLine(eventArgs.Data);
            process.ErrorDataReceived += (sender, eventArgs) => outputStringBuilder.AppendLine(eventArgs.Data);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            try
            {
                var processExited = process.WaitForExit(Emulator.Timeout);

                if(!processExited) // we timed out...
                {
                    process.Kill();
                    IsInstalled = true;
                    IsSuccessful = false;
                    Error = @$"Process timed-out waiting for a response from {Emulator.EmulatorExeName}.";
                    return;
                }

                var output = outputStringBuilder.ToString();
                Output = output;

                if(process.ExitCode != 0)
                {
                    switch(action)
                    {
                        case EmulatorAction.Start:
                            if(process.ExitCode == -5)
                            {
                                IsSuccessful = true;
                                IsRunning = true;
                                Warning = @"Azure storage emulator already in a running state.";
                                return;
                            }

                            break;

                        case EmulatorAction.Stop:
                            if(process.ExitCode == -6)
                            {
                                IsSuccessful = true;
                                IsRunning = false;
                                Warning = @"Azure storage emulator already in a stopped state.";
                                return;
                            }

                            break;

                        case EmulatorAction.Clear:
                            break;

                        case EmulatorAction.Init:
                            break;
                    }

                    var errorMessage = @$"{Emulator.EmulatorExeName} exited with a non-zero error code: {process.ExitCode}.";
                    if(output.Length > 0)
                    {
                        errorMessage = errorMessage + $@"  Output: {output}.";
                    }

                    IsInstalled = true;
                    IsSuccessful = false;
                    Error = errorMessage;
                }

                IsSuccessful = true;

                /*
                 * For example:
                 *     Windows Azure Storage Emulator 5.10.0.0 command line tool
                 *     IsRunning: True
                 *     BlobEndpoint: http://127.0.0.1:10000/
                 *     QueueEndpoint: http://127.0.0.1:10001/
                 *     TableEndpoint: http://127.0.0.1:10002/
                 */

                var lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

                var index = 0;
                foreach(var line in lines)
                {
                    Match matches;
                    switch(index++)
                    {
                        case 0:

                            matches = _versionRegex.Match(line);
                            if(matches.Success)
                            {
                                Version = new Version(matches.Groups[1].Value);
                            }

                            break;

                        case 1:
                            matches = _isRunningRegex.Match(line);
                            if(matches.Success)
                            {
                                if(bool.TryParse(matches.Groups[1].Value, out var isRunning))
                                {
                                    IsRunning = isRunning;
                                }
                            }

                            break;

                        case 2:
                            matches = _blobEndpointRegex.Match(line);
                            if(matches.Success)
                            {
                                BlobEndpoint = new Uri(matches.Groups[1].Value, UriKind.Absolute);
                            }

                            break;

                        case 3:
                            matches = _queueEndpointRegex.Match(line);
                            if(matches.Success)
                            {
                                QueueEndpoint = new Uri(matches.Groups[1].Value, UriKind.Absolute);
                            }

                            break;

                        case 4:
                            matches = _tableEndpointRegex.Match(line);
                            if(matches.Success)
                            {
                                TableEndpoint = new Uri(matches.Groups[1].Value, UriKind.Absolute);
                            }

                            break;

                        default:
                            if(string.IsNullOrWhiteSpace(Error))
                            {
                                Warning = Warning + @$"; Unexpected results on line {index}: {line}";
                            }
                            else
                            {
                                Warning = @$"Unexpected results on line {index}: {line}";
                            }

                            break;
                    }
                }
            }
            finally
            {
                process.Close();
            }
        }

        /// <summary>Gets the action that was performed to populate this instance.</summary>
        /// <value>The action that was performed to populate this instance.</value>
        public EmulatorAction Action { get; }

        /// <summary>Gets the URL of the blob storage endpoint.</summary>
        /// <value>
        ///     The URL of the blob storage endpoint. This will be <see langword="null"/> if the Azure Storage Emulator could not be found or the execution was
        ///     not successful.
        /// </value>
        public Uri? BlobEndpoint { get; }

        /// <summary>Gets a string describing any errors that occurred during the execution of the Azure Storage Emulator.</summary>
        /// <value>A describing any errors that occurred during the execution of the Azure Storage Emulator; or <see langword="null"/> if none were found.</value>
        public string? Error { get; }

        /// <summary>Gets a value indicating whether the Azure Storage Emulator is installed.</summary>
        /// <value>
        ///     <see langword="true"/> if the Azure Storage Emulator is installed (i.e. the executables were found in the expected location); otherwise,
        ///     <see langword="false"/>.
        /// </value>
        public bool IsInstalled { get; }

        /// <summary>Gets a value indicating whether the Azure Storage Emulator the is running.</summary>
        /// <value>
        ///     Gets <see langword="true"/> if the Azure Storage Emulator is definitely running; <see langword="false"/> if the emulator is definitely not
        ///     running; or <see langword="null"/> if the status of the emulator could not be determined.
        /// </value>
        public bool? IsRunning { get; }

        /// <summary>Gets a value indicating whether the call to the Azure Storage Emulator was executed without error.</summary>
        /// <value><see langword="true"/> if the call to the Azure Storage Emulator was executed without error; otherwise, <see langword="false"/>.</value>
        public bool IsSuccessful { get; }

        /// <summary>Gets all the output from running the Azure Storage Emulator command.</summary>
        /// <value>All the output from running the Azure Storage Emulator command.</value>
        public string? Output { get; }

        /// <summary>Gets the URL of the queue endpoint.</summary>
        /// <value>
        ///     The URL of the queue endpoint. This will be <see langword="null"/> if the Azure Storage Emulator could not be found or the execution was not
        ///     successful.
        /// </value>
        public Uri? QueueEndpoint { get; }

        /// <summary>Gets the URL of the table endpoint.</summary>
        /// <value>
        ///     The URL of the table endpoint. This will be <see langword="null"/> if the Azure Storage Emulator could not be found or the execution was not
        ///     successful.
        /// </value>
        public Uri? TableEndpoint { get; }

        /// <summary>Gets the version of the Azure Storage Emulator installed.</summary>
        /// <value>
        ///     The version of the Azure Storage Emulator installed.  This will be <see langword="null"/> if the Azure Storage Emulator could not be found or the
        ///     execution was not successful.
        /// </value>
        public Version? Version { get; }

        /// <summary>Gets a string describing any warnings that occurred during the execution of the Azure Storage Emulator.</summary>
        /// <value>A describing any warnings that occurred during the execution of the Azure Storage Emulator; or <see langword="null"/> if none were found.</value>
        public string? Warning { get; }
    }
}