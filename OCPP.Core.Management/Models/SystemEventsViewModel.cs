using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;
using OCPP.Core.Database;

namespace OCPP.Core.Management.Models
{
    public class SystemEventsViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        
        public string SelectedChargePointId { get; set; }
        public string SelectedResult { get; set; }
        public int? SelectedConnectorId { get; set; }

        public List<SelectListItem> ChargePointList { get; set; }
        public List<SelectListItem> ResultList { get; set; }
        public List<SelectListItem> ConnectorList { get; set; }

        public List<MessageLog> Events { get; set; }

        // Pagination
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalItems { get; set; }
        public int PageSize { get; set; }
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;

        public SystemEventsViewModel()
        {
            ChargePointList = new List<SelectListItem>();
            ResultList = new List<SelectListItem>();
            ConnectorList = new List<SelectListItem>();
            Events = new List<MessageLog>();
            CurrentPage = 1;
            PageSize = 50;
        }
    }
}
