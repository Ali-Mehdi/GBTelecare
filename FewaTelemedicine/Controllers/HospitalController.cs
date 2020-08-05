﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FewaTelemedicine.Common;
using FewaTelemedicine.Domain;
using FewaTelemedicine.Domain.Models;
using FewaTelemedicine.Domain.Repositories;
using FewaTelemedicine.Domain.Services;
using FewaTelemedicine.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace FewaTelemedicine.Controllers
{
    public class HospitalController : Controller
    {
        private readonly ILogger<HospitalController> _logger;
        private readonly IDoctorRepository _doctorRepository;
        List<DoctorCabin> _doctorcabins = null;

        WaitingRoom _waitingroom = null;
        private readonly IPatientRepository _patientRepository;
        List<DoctorsModel> _doctorsmodels = null;
        private int idletime = 0;
        private readonly IHubContext<NotificationHub, INotificationHub> _notify;
        private readonly IConfiguration _config;
        private  FewaDbContext FewaDbContext = null;
        public IConfiguration Configuration { get; }

        public HospitalController(
            ILogger<HospitalController> logger,
            List<DoctorCabin> doctorcabins,
            WaitingRoom waitingroom,
            IConfiguration configuration,
            List<DoctorsModel> doctorsmodels,
            IHubContext<NotificationHub, INotificationHub> notify,
            IConfiguration config,IPatientRepository patientRepository,
             IDoctorRepository doctorRepository,
            FewaDbContext  fewaDbContext)
        {
            FewaDbContext = fewaDbContext;
            _patientRepository = patientRepository;
            _doctorsmodels = doctorsmodels;
            Configuration = configuration;
            _logger = logger;
            _doctorcabins = doctorcabins;
            _waitingroom = waitingroom;
            _doctorRepository = doctorRepository;
            idletime = Convert.ToInt32(configuration["IdleTime"]);
            _notify = notify;
            _config = config;
        }
        public IActionResult Index()
        {
            return View();
        }
        public IActionResult GetHospitalParams()
        {
            try
            {
                string[] parameters = { "Description", "Name", "ContactNumber", "Email", "LogoPath" };
                List<string> paramsList = new List<string>(parameters);

                List<ParametersModel> result = FewaDbContext.ParametersModels.Where(a => a.ParameterGroupName == "Hospital" && paramsList.Any(b => b == a.ParameterName)).ToList();
                return Ok(Json(result));
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Internal server error.");
            }

        }
        [AllowAnonymous]
        public IActionResult LoginPatient([FromBody] PatientsAttendedModel obj)
        {
            if (!(getPatientbyName(obj.PatientName) is null))
            {
                return StatusCode(500, "Patient already logged in");
            }
            obj.LastUpdated = DateTime.Now;
            _waitingroom.Patients.Add(obj);
           

            var token = GenerateJSONWebToken(obj.PatientName,"Patient");
            var result = new
            {
                User = obj,
                Token = token
            };
            return Ok(Json(result));
            // return Ok(Json(obj));
        }
        public IActionResult WaitingRoom()
        {
            return Json(_waitingroom);
        }
        [AllowAnonymous]
        //public IActionResult LoginDoctor([FromBody] DoctorsModel obj)
        //{
        //    //if (CheckDoctor(obj.Name, obj.Password))
        //    {
        //        HttpContext.Session.SetString("Name", obj.UserName);
        //        var token = GenerateJSONWebToken(obj.UserName,"Patient");
        //        AddDoctorCabin(obj.UserName);
        //        var result = new
        //        {
        //            User = obj,
        //            Token = token
        //        };
        //        return Ok(Json(result));
        //    }
        //    //else
        //    //{
        //    //    return StatusCode(401);
        //    //}

        //}
        public bool CheckDoctor(string name, string password)
        {
            foreach (var item in _doctorsmodels)
            {
                if (item.UserName == name)
                {
                    if (item.Password == password)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            return false;
        }
        private void AddDoctorCabin(string DoctorName)
        {
            foreach (var item in _doctorcabins)
            {
                if (item.DoctorsModel.UserName == DoctorName)
                {
                    _doctorcabins.Remove(item);
                    _doctorcabins.Add(new DoctorCabin()
                    { DoctorsModel = new DoctorsModel() { UserName = DoctorName } });
                    return;
                }
            }
            _doctorcabins.Add(new DoctorCabin()
            { DoctorsModel = new DoctorsModel() { UserName = DoctorName } });

        }
        public List<PatientsAttendedModel> GetPatientsAttended()
        {
            var attendedPatients = (from temp in FewaDbContext.PatientsAttendedModels
                                    orderby temp.StartTime descending
                                    select temp
                                    ).ToList<PatientsAttendedModel>();
            return attendedPatients;
        }

    public IActionResult GetUpdatedDoctor()
        {
            string username = HttpContext.Session.GetString("Name");           
            var doctorProfile = (from temp in FewaDbContext.DoctorsModels
                                 where temp.UserName == username
                                 select temp).FirstOrDefault();
            return Ok(doctorProfile);
        }
        public IActionResult GetDoctorCabin()
        {
            return Json(getCurrentDoctorCabin());
        }
        private DoctorCabin getCurrentDoctorCabin()
        {
            foreach (var item in _doctorcabins)
            {
                if (item.DoctorsModel.UserName == HttpContext.Session.GetString("Name"))
                {
                    return item;
                }

            }
            return null;
        }
        public IActionResult CallPatient([FromBody]PatientsAttendedModel obj)
        {
            PatientsAttendedModel p = getPatientbyName(obj.PatientName);
            if (p is null)
            {
                return StatusCode(500);
            }
            else
            {
                p.Status = (int)TeleConstants.PatientCalled;
                //p.DoctorNameAttending = HttpContext.Session.GetString("Name");
                p.LastUpdated = DateTime.Now;
                getCurrentDoctorCabin().PatientsAttendedModel = p;
                //var dd = JsonSerializer.Serialize(_waitingroom.PatientsAttendedModels);
                //_notify.Clients.All.BroadcastMessage("PatientLoggedIn", dd);
                //var patient = JsonSerializer.Serialize(p);
                //_notify.Clients.All.BroadcastMessage("CallPatient", patient);
                return Ok(p);
            }
        }
        private PatientsAttendedModel getPatientbyName(string PatName)
        {
            foreach (var t in _waitingroom.Patients)
            {
                if (PatName == t.PatientName)
                {
                    return t;
                }
            }
            return null;
        }
        public IActionResult CurrentPatients()
        {

            this.RemoveIdle();
            //var dd = JsonSerializer.Serialize(_waitingroom.PatientsAttendedModels);
            //_notify.Clients.All.BroadcastMessage("PatientLoggedIn", dd);
            return Json(_waitingroom.Patients);
        }
        public IActionResult WriteMedication([FromBody]PatientsAttendedModel obj)
        {
            PatientsAttendedModel p = getPatientbyName(getCurrentDoctorCabin().PatientsAttendedModel.PatientName);
            if (p.Status == (int)TeleConstants.PatientCalled)
            {
                p.Status = (int)TeleConstants.PatientCompleted;
                p.LabOrdersSent = obj.LabOrdersSent;
                p.NewPrescriptionsSentToYourPharmacy = obj.NewPrescriptionsSentToYourPharmacy;
                p.NewPrescriptionsMailedToYou = obj.NewPrescriptionsMailedToYou;
                p.Medication = obj.Medication;
                p.FollowUpNumber = obj.FollowUpNumber;
                p.FollowUpMeasure = obj.FollowUpMeasure;
                p.Status = (int)TeleConstants.PatientCompleted;
                return Ok(true); 
            }
            else
            {
                return Ok(true);
            }
        }
        public IActionResult TakeFinalReport([FromBody]PatientsAttendedModel p1)
        {
            PatientsAttendedModel p = getPatientbyName(p1.PatientName);
            if (p is null) { return Ok(null); }
            if (p.Status == (int)TeleConstants.PatientCompleted)
            {
                var patient = JsonSerializer.Serialize(p);

                //_notify.Clients.All.BroadcastMessage("PatientCompleted", patient);
                //_waitingroom.PatientsAttendedModels.Remove(p);

                return Ok(p);
            }
            else
            {
                return Ok(null);
            }
        }
        public IActionResult PatientAttended([FromBody]PatientsAttendedModel obj)
        {
            PatientsAttendedModel p = getPatientbyName(obj.PatientName);
            if (p is null)
            {
                return StatusCode(500);
            }
            else
            {
                getCurrentDoctorCabin().PatientsAttendedModel = new PatientsAttendedModel();
                p.Status = (int)TeleConstants.PatientCompleted;
                p.LabOrdersSent = obj.LabOrdersSent;
                p.NewPrescriptionsSentToYourPharmacy = obj.NewPrescriptionsSentToYourPharmacy;
                p.NewPrescriptionsMailedToYou = obj.NewPrescriptionsMailedToYou;
                p.Medication = obj.Medication;
                p.FollowUpNumber = obj.FollowUpNumber;
                p.FollowUpMeasure = obj.FollowUpMeasure;                
                _waitingroom.Patients.Remove(p);

                //var dd = JsonSerializer.Serialize(_waitingroom.PatientsAttendedModels);
                //_notify.Clients.All.BroadcastMessage("PatientLoggedIn", dd);

                //var patient = JsonSerializer.Serialize(p);
                //_notify.Clients.All.BroadcastMessage("PatientCompleted", patient);
                RemoveIdle();


                return Ok(p);
            }
        }
        private void RemoveIdle()
        {
            var removepats = new List<PatientsAttendedModel>();

            foreach (var t in _waitingroom.Patients)
            {
                var diffInSeconds = DateTime.Now.Subtract(t.LastUpdated).TotalSeconds;
                t.TotalCheckupTime = diffInSeconds;
                if (diffInSeconds > idletime)
                {
                    removepats.Add(t);
                }
            }
            foreach (var t in removepats)
            {
                _waitingroom.Patients   .Remove(t);
            }
        }

        public IActionResult UpdateProfile([FromBody] DoctorsModel obj)
        {
            var doc = _doctorRepository.GetDoctorByUserName(obj.UserName);
            if (doc is null)
            {
                return StatusCode(500);
            }
            else
            {
                doc.NameTitle = obj.NameTitle;
                doc.DoctorName = obj.DoctorName;
                doc.Email = obj.Email;
                doc.MobileNumber = obj.MobileNumber;
                doc.Designation = obj.Designation;
                doc.MedicalDegree = obj.MedicalDegree;
                doc.Clinic = obj.Clinic;
                doc.Password = obj.Password;
                doc.Image = obj.Image;
            }
            FewaDbContext.DoctorsModels.Update(doc);
            FewaDbContext.SaveChanges();
            return Ok(doc);
        }

        public async Task<IActionResult> UploadImage()
        {
            try
            {
                string username = HttpContext.Session.GetString("Name");
                var user = JsonSerializer.Deserialize<DoctorsModel>(Request.Form["user"].ToString());               
                var file = Request.Form.Files[0];
                var doc = _doctorRepository.GetDoctorByUserName(user.UserName);
                if (doc is null)
                {
                    return StatusCode(500);
                }

                using (var memoryStream = new MemoryStream())
                {
                    await file.CopyToAsync(memoryStream);

                    // Upload the file if less than 2 MB
                    if (memoryStream.Length < 2097152)
                    {
                        doc.Image = memoryStream.ToArray();
                        //_fewaDbContext.DoctorsModels.Update(doc);
                        //await _fewaDbContext.SaveChangesAsync();
                    }
                    else
                    {
                        ModelState.AddModelError("File", "The file is too large.");
                    }
                    return Ok(doc.Image);
                }
            }
            catch (System.Exception ex)
            {
                return Ok("Upload Failed: " + ex.Message);
            }
        }

        [HttpGet("GetImage")]
        public IActionResult GetImage()
        {
            string username = HttpContext.Session.GetString("Name");
            var doc = _doctorRepository.GetDoctorByUserName(username);
            if (doc.Image != null)
            {
                //string base64Data = Convert.ToBase64String(doc.Image);
                //var imageURL = string.Format("data:image/png;base64,{0}", base64Data);
                return Ok(doc);
            }
            return StatusCode(500);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private string GenerateJSONWebToken(string username, string usertype)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
            var claims = new[] {
                new Claim("Issuer", _config["Jwt:Issuer"]),
                new Claim("UserType",usertype),
                new Claim(JwtRegisteredClaimNames.UniqueName, username)
            };

            var token = new JwtSecurityToken(_config["Jwt:Issuer"],
              _config["Jwt:Issuer"],
              claims,
              expires: DateTime.Now.AddMinutes(120),
              signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}