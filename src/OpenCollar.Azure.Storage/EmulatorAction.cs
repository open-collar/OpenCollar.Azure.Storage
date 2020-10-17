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

namespace OpenCollar.Azure.Storage
{
    /// <summary>Defines the actions that can be performed by the Azure Storage Emulator command.</summary>
    public enum EmulatorAction
    {
        /// <summary>
        ///     The action is unknown or undefined.  This is a sentinel used to prevent accidental use of uninitialized values.  Usage of this value will typical
        ///     result in an exception.
        /// </summary>
        Unknown = 0,

        /// <summary>The call retrieves the current status of the Azure Storage Emulator.</summary>
        Status,

        /// <summary>The call attempts to start the Azure Storage Emulator.</summary>
        Start,

        /// <summary>/// The call attempts to stop the Azure Storage Emulator.</summary>
        Stop,

        /// <summary>/// The call attempts to clear all data from the Azure Storage Emulator.</summary>
        Clear,

        /// <summary>/// The call attempts to initialize the Azure Storage Emulator.</summary>
        Init
    }
}