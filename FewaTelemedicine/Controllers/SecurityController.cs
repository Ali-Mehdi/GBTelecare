﻿using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using FewaTelemedicine.Domain.Models;
using FewaTelemedicine.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace FewaTelemedicine.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SecurityController : ControllerBase
    {
        private readonly IDoctorRepository _doctorRepository;
        List<DoctorCabin> _doctorcabins = null;
        List<DoctorsModel> _doctors = null;
        private readonly IConfiguration _config;

        public SecurityController(
            IDoctorRepository doctorRepository,
            List<DoctorCabin> doctorcabins, IConfiguration config, List<DoctorsModel> doctors
            )
        {
            _doctorRepository = doctorRepository;
            _doctorcabins = doctorcabins;
            _doctors = doctors;
            _config = config;
        }

        [HttpGet]
        public ActionResult GetDoctors()
        {
            return Ok(_doctorRepository.GetDoctorsList());
        }



        [HttpPost("Login")]
        public ActionResult Login(DoctorsModel doctor)
        {
            try
            {
                if (doctor == null)
                {
                    return BadRequest();
                }
                if (string.IsNullOrEmpty(doctor.UserName))
                {
                    return BadRequest();
                }
                var doc = _doctorRepository.GetDoctorByUserName(doctor.UserName);
                if (doc == null)
                {
                    return Unauthorized();
                }
                //if (doc.Password == doctor.Password)
                //{
                doctor.Image = doc.Image;
                doctor.NameTitle = doc.NameTitle;
                doctor.DoctorName = doc.DoctorName;
                HttpContext.Session.SetString("Name", doctor.UserName);
                    var token = GenerateJSONWebToken(doctor.UserName, "doctor");
                    AddDoctorCabin(doc.UserName);
                    var data = new
                    {
                        User = doctor,
                        Token = token
                    };
                    return Ok(data);
                //}
               // return Unauthorized();
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex);
            }
        }
        //public bool CheckDoctor(string name, string password)
        //{
        //    foreach (var item in _doctors)
        //    {
        //        if (item.UserName == name)
        //        {
        //            if (item.Password == password)
        //            {
        //                return true;
        //            }
        //            else
        //            {
        //                return false;
        //            }
        //        }
        //    }
        //    return false;
        //}
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

        [HttpPost("UpdateProfile")]
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
            }
            return Ok(doc);
        }
    }
}