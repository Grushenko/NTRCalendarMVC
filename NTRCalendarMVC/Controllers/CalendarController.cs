﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using log4net;
using NTRCalendarMVC.ViewModels;

namespace NTRCalendarMVC.Controllers {
    public class CalendarController : Controller {
        ILog log = log4net.LogManager.GetLogger(typeof(CalendarController).ToString());

        private StorageContext db = new StorageContext();

        public CalendarController() { }

        public CalendarController(StorageContext pdb) {
            db = pdb;
        }

        // GET: Calendar
        public ActionResult Index(DateTime? firstDay) {
            string userID = (string) Session["UserID"];
            if (userID == null) return RedirectToAction("Index", "Home");

            var user = db.People.FirstOrDefault(p => p.UserID.Equals(userID));
            var weeks = new List<Week>(4);
            var day = firstDay ?? DateTime.Today;
            while (day.DayOfWeek != DayOfWeek.Monday) day = day.AddDays(-1);
            var date = day;

            var appointments = db.Attendances
                .Where(a => a.Person.UserID.Equals(userID))
                .Select(a => a.Appointment)
                .ToList();

            for (var weekNo = 0; weekNo < 4; ++weekNo) {
                var w = new Week {
                    Year = day.Year,
                    Number = new GregorianCalendar().GetWeekOfYear(day, CalendarWeekRule.FirstDay, DayOfWeek.Monday),
                    Days = new List<Day>()
                };

                for (var dayOfWeek = 0; dayOfWeek < 7; dayOfWeek++) {
                    var d = new Day {
                        Date = day,
                        Appointments = new List<Appointment>(
                            appointments
                                .Where(a => a.AppointmentDate.Equals(day))
                                .OrderBy(a => a.StartTime)
                                .ToList())
                    };
                    w.Days.Add(d);
                    day = day.AddDays(1);
                }
                weeks.Add(w);
            }

            var model = new CalendarViewModel {
                FirstDay = date,
                Today = DateTime.Today,
                Weeks = weeks,
                User = $"{user.FirstName} {user.LastName}"
            };

            log.InfoFormat("Rendered calendar grid from {0} for {1}", date, userID);

            return View(model);
        }

        public ActionResult Prev(DateTime day) {
            var firstDay = day.AddDays(-7);
            return RedirectToAction("Index", new {firstDay = firstDay});
        }

        public ActionResult Next(DateTime day) {
            var firstDay = day.AddDays(7);
            return RedirectToAction("Index", new {firstDay = firstDay});
        }


        //DETAILS
        public ActionResult Details(Guid? id) {
            if (id == null) {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Appointment appointment = db.Appointments.Find(id);
            if (appointment == null) {
                return RedirectToAction("Index");
            }
            log.InfoFormat("Rendered details for {0}", appointment);
            return View(appointment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Details(
            [Bind(Include = "AppointmentID,Title,Description,AppointmentDate,StartTime,EndTime,timestamp")]
            Appointment appointment) {
            if (ModelState.IsValid) {
                db.Entry(appointment).State = EntityState.Modified;
                try {
                    db.SaveChanges();
                    log.InfoFormat("Changed {0}", appointment);
                    return RedirectToAction("Index");
                }
                catch (DbUpdateConcurrencyException) {
                    ModelState.AddModelError(string.Empty,
                        "Nie można zapisać, spotkanie zmodyfikowane w innej sesji aplikacji.");
                }
                catch (OverflowException) {
                    ModelState.AddModelError(string.Empty, "Format godziny jest niepoproawny");
                }
                catch (Exception) {
                    ModelState.AddModelError(string.Empty, "Wystąpił błąd");
                }
            }
            return View(appointment);
        }

        //CREATE
        public ActionResult Create(DateTime day) {
            var appointment = new Appointment {AppointmentDate = day};
            return View(appointment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(
            [Bind(Include = "AppointmentID,Title,Description,AppointmentDate,StartTime,EndTime,timestamp")]
            Appointment appointment) {
            string userID = (string) Session["UserID"];
            if (userID == null) return RedirectToAction("Index", "Home");

            if (ModelState.IsValid) {
                appointment.AppointmentID = Guid.NewGuid();
                db.Appointments.Add(appointment);
                var person = db.People.FirstOrDefault(p => p.UserID.Equals(userID));
                if (person != null) {
                    var att = new Attendance {
                        AttendanceID = Guid.NewGuid(),
                        PersonID = person.PersonID,
                        AppointmentID = appointment.AppointmentID,
                    };
                    db.Attendances.Add(att);
                }
                db.SaveChanges();
                log.InfoFormat("Created {0}", appointment);
                return RedirectToAction("Index");
            }

            return View(appointment);
        }


        public ActionResult Delete(Guid? id, bool? error) {
            if (id == null)
                return RedirectToAction("Index");

            var appointment = db.Appointments.Find(id);

            if (appointment == null)
                return RedirectToAction("Index");

            if (error.GetValueOrDefault()) {
                ViewBag.ConcurrencyErrorMessage =
                    "Spotkanie zostało zmodyfikowane w innej instancji. Jeżeli mimo wszystko chcesz usunąć, potwierdź.";
            }

            return View(appointment);
        }

        [HttpPost]
//        [ValidateAntiForgeryToken]
        public ActionResult Delete(Appointment appointment) {
            string userID = (string) Session["UserID"];
            if (userID == null) return RedirectToAction("Index", "Home");

            try {
                var app = db.Entry(appointment);
                var attendances = db.Attendances.Where(a => a.AppointmentID.Equals(appointment.AppointmentID)).ToList();

                var att = attendances.FirstOrDefault(a => a.Person.UserID.Equals(userID));
                if (att != null) db.Attendances.Remove(att);

                if (attendances.Count <= 1)
                    db.Appointments.Remove(app.Entity);
//                    app.State = EntityState.Deleted;
                   

                db.SaveChanges();
                log.InfoFormat("Deleted {0} for {1}", appointment, userID);
                return RedirectToAction("Index");
            }

            catch (DBConcurrencyException e) {
                ModelState.AddModelError(string.Empty,
                    "Nie można zapisać, spotkanie usunięte lub zmienione w innej sesji aplikacji.");
            }
            catch (Exception) {
                ModelState.AddModelError(string.Empty, "Niespodziewany błąd");
            }

            return RedirectToAction("Delete", new {id = appointment.AppointmentID, error = true});
        }
    }
}