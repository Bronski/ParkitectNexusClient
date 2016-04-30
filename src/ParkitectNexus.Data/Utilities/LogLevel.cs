// ParkitectNexusClient
// Copyright (C) 2016 ParkitectNexus, Tim Potze
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

namespace ParkitectNexus.Data.Utilities
{
    /// <summary>
    ///     Contains logging levels.
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        ///     Debugging log message.
        /// </summary>
        Debug,

        /// <summary>
        ///     Informative log message.
        /// </summary>
        Info,

        /// <summary>
        ///     Warning log message.
        /// </summary>
        Warn,

        /// <summary>
        ///     Error log message.
        /// </summary>
        Error,

        /// <summary>
        ///     Fatal error log message.
        /// </summary>
        Fatal
    }
}