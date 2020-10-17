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
using System.IO;

using JetBrains.Annotations;

namespace OpenCollar.Azure.Storage
{
    /// <summary>Wraps up the functionality of the Azure Storage Emulator command line tool.</summary>
    [DebuggerDisplay("Azure Storage Emulator {((Version != null) ? \"(\" + nameof(Version) + \")\" : string.Empty)}")]
    public sealed class Emulator
    {
        /// <summary>The name of the Azure storage emulator executable.</summary>
        public const string EmulatorExeName = @"AzureStorageEmulator.exe";

        /// <summary>The period of time that the emulator EXE will be allowed to run before the attempt is aborted.</summary>
        public const int Timeout = 30_000 /* ms */;

        /// <summary>Initializes a new instance of the <see cref="Emulator"/> class.</summary>
        public Emulator()
        {
            // C:\Program Files (x86)\Microsoft SDKs\Azure\Storage Emulator\AzureStorageEmulator.exe
            SdkPath = Path.GetFullPath(Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? string.Empty, "Microsoft SDKs", "Azure"));
            IsSdkPresent = Directory.Exists(SdkPath);
            if(!IsSdkPresent)
            {
                Console.WriteLine(@$"Unable to find Azure SDK: ""{SdkPath}"".");
            }

            EmulatorDirectoryPath = Path.GetFullPath(Path.Combine(SdkPath, "Storage Emulator"));
            IsEmulatorDirectoryPresent = Directory.Exists(EmulatorDirectoryPath);
            if(!IsEmulatorDirectoryPresent)
            {
                Console.WriteLine(@$"Unable to find Storage Emulator directory in Azure SDK: ""{EmulatorDirectoryPath}"".");
            }

            EmulatorExePath = Path.GetFullPath(Path.Combine(EmulatorDirectoryPath, EmulatorExeName));
            IsEmulatorExePresent = File.Exists(EmulatorExePath);
            if(!IsEmulatorExePresent)
            {
                Console.WriteLine(@$"Unable to find Storage Emulator executable in Azure SDK: ""{EmulatorExePath}"".");
            }
        }

        /// <summary>Gets the most recently returned URL of the blob storage endpoint.</summary>
        /// <value>
        ///     The most recently returned URL of the blob storage endpoint. This will be <see langword="null"/> if the Azure Storage Emulator could not be found
        ///     or the execution was not successful.
        /// </value>
        public Uri? BlobEndpoint { get; private set; }

        /// <summary>Gets the Azure storage emulator directory path.</summary>
        /// <value>The Azure storage emulator directory path.</value>
        public string EmulatorDirectoryPath { get; }

        /// <summary>Gets the Azure storage emulator executable path.</summary>
        /// <value>The Azure storage emulator executable path.</value>
        public string EmulatorExePath { get; }

        /// <summary>Gets a value indicating the Azure storage emulator directory is present.</summary>
        /// <value><see langword="true"/> if the Azure storage emulator directory is present; otherwise, <see langword="false"/>.</value>
        public bool IsEmulatorDirectoryPresent { get; }

        /// <summary>Gets a value indicating the Azure storage emulator executable is present.</summary>
        /// <value><see langword="true"/> if the Azure storage emulator executable is present; otherwise, <see langword="false"/>.</value>
        public bool IsEmulatorExePresent { get; }

        /// <summary>Gets a value indicating whether the Azure SDK is present.</summary>
        /// <value><see langword="true"/> if the Azure SDK is present; otherwise, <see langword="false"/>.</value>
        public bool IsSdkPresent { get; }

        /// <summary>Gets the most recently returned URL of the queue endpoint.</summary>
        /// <value>
        ///     The most recently returned URL of the queue endpoint. This will be <see langword="null"/> if the Azure Storage Emulator could not be found or the
        ///     execution was not successful.
        /// </value>
        public Uri? QueueEndpoint { get; private set; }

        /// <summary>Gets the Azure SDK path.</summary>
        /// <value>The Azure SDK path.</value>
        public string SdkPath { get; }

        /// <summary>Gets the most recently returned URL of the table endpoint.</summary>
        /// <value>
        ///     The most recently returned URL of the table endpoint. This will be <see langword="null"/> if the Azure Storage Emulator could not be found or the
        ///     execution was not successful.
        /// </value>
        public Uri? TableEndpoint { get; private set; }

        /// <summary>Gets the most recently returned version of the Azure Storage Emulator installed.</summary>
        /// <value>
        ///     The most recently returned version of the Azure Storage Emulator installed.  This will be <see langword="null"/> if the Azure Storage Emulator
        ///     could not be found or the execution was not successful.
        /// </value>
        public Version? Version { get; private set; }

        /// <summary>Delete all data in the emulator.</summary>
        /// <returns>The current status of the storage emulator.</returns>
        [NotNull]
        public EmulatorStatus Clear() => Run(EmulatorAction.Clear);

        /// <summary>Starts the Azure Storage emulator if it not already running, and if the host application is running on a developers desktop.</summary>
        /// <param name="isInAzure">A (optional) flag that can be used to signal that the application is running within Azure.</param>
        public static void StartEmulatorIfRequired(bool? isInAzure = null)
        {
            if(!Debugger.IsAttached || (isInAzure.HasValue && isInAzure.Value))
            {
                return;
            }

            // Start the Storage Emulator on a developers desktop if necessary/possible.
            var emulator = new Emulator();
            var isRunning = emulator.Status().IsRunning;
            if(isRunning.HasValue && isRunning.Value)
            {
                Debug.WriteLine(@"Azure Storage Emulator already running.");
            }
            else
            {
                isRunning = emulator.Start();
                if(isRunning.HasValue)
                {
                    Debug.WriteLine($@"Azure Storage Emulator has {(isRunning.Value ? string.Empty : "NOT ")}started.");
                }
                else
                {
                    Debug.WriteLine(@"Unable to determine whether Azure Storage Emulator has started.");
                }
            }
        }

        /// <summary>Initialize the emulator database and configuration.</summary>
        /// <returns>The current status of the storage emulator.</returns>
        [NotNull]
        public EmulatorStatus Init() => Run(EmulatorAction.Init);

        /// <summary>Ensures the Azure Storage Emulator is started.</summary>
        /// <returns><see langword="true"/> if the emulator is running; otherwise, <see langword="false"/> if it is not run.</returns>
        public bool? Start()
        {
            var status = Run(EmulatorAction.Status);

            if(status.IsRunning.HasValue && status.IsRunning.Value)
            {
                return true;
            }

            status = Run(EmulatorAction.Start);

            return status.IsRunning;
        }

        /// <summary>Gets the current status of the Azure Storage Emulator.</summary>
        /// <returns>The current status of the Azure Storage Emulator.</returns>
        [NotNull]
        public EmulatorStatus Status() => Run(EmulatorAction.Status);

        /// <summary>Ensures the Azure Storage Emulator is stopped.</summary>
        /// <returns><see langword="true"/> if the emulator is running; otherwise, <see langword="false"/>.</returns>
        public bool? Stop()
        {
            var status = Run(EmulatorAction.Status);

            if(status.IsRunning.HasValue && !status.IsRunning.Value)
            {
                return false;
            }

            status = Run(EmulatorAction.Stop);

            return status.IsRunning;
        }

        /// <summary>Runs the specified action.</summary>
        /// <param name="action">The action.</param>
        /// <returns>Returns the current status of the storage emulator.</returns>
        [NotNull]
        private EmulatorStatus Run(EmulatorAction action)
        {
            var status = new EmulatorStatus(this, action);

            if(!ReferenceEquals(status.Version, null))
            {
                Version = status.Version;
            }

            if(!ReferenceEquals(status.BlobEndpoint, null))
            {
                BlobEndpoint = status.BlobEndpoint;
            }

            if(!ReferenceEquals(status.QueueEndpoint, null))
            {
                QueueEndpoint = status.QueueEndpoint;
            }

            if(!ReferenceEquals(status.TableEndpoint, null))
            {
                TableEndpoint = status.TableEndpoint;
            }

            return status;
        }
    }
}