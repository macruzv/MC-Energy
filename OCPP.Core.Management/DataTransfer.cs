/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * Copyright (C) 2020-2021 dallmann consulting GmbH.
 * All Rights Reserved.
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
 using Newtonsoft.Json.Linq;

namespace OCPP.Core.Server.Messages_OCPP16
{
    /// <summary>
    /// Defines the request payload for a DataTransfer message
    /// </summary>
    public class DataTransferRequest
    {
        public string vendorId { get; set; }

        public string messageId { get; set; }

        /// <summary>
        /// The data to be transferred. Changed from string to object to support complex JSON objects.
        /// </summary>
        public object ?data { get; set; }
    }

    /// <summary>
    /// Defines the confirmation payload for a DataTransfer message
    /// </summary>
    public class DataTransferResponse
    {
        public string status { get; set; }

        public object ?data { get; set; }
    }
}