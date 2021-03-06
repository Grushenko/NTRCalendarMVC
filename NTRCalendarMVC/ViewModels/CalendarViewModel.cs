﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NTRCalendarMVC.ViewModels {
    public class CalendarViewModel {
        public DateTime FirstDay { get; set; }
        public DateTime Today { get; set; }
        public List<Week> Weeks { get; set; }
        public string User { get; set; }
    }
}