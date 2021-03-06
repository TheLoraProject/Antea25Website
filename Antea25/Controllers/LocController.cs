﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Antea25.Data;
using Antea25.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Antea25.Controllers
{
    public class LocController : Controller
    {
        private readonly ApplicationDbContext DbContext;

        public LocController([FromServices] ApplicationDbContext appDbContext)
        {
            DbContext = appDbContext;
        }

        // GET: Localisation
        public ActionResult Index()
        {
            if (User.Identity.IsAuthenticated)
            {
                return View();
            }
            return RedirectToAction("Login", "Account");
        }

        #region method for Application

         ///If called by App, we need to pass the userId as argument
        [HttpGet]
        [Route("/api/[controller]/GetGpsData/{userId}")]
        public List<GpsPosition> GetGpsData(string userId)
        {
            if (User.Identity.IsAuthenticated)
            {
                // return DbContext.GpsPosition.Where(p => p.UserId == User.Claims.FirstOrDefault().Value).OrderByDescending(p=>p.GpsPositionDate).ToList();
                return DbContext.GpsPosition.Where(p => p.Device.UserId == User.Claims.FirstOrDefault().Value).OrderByDescending(p=>p.GpsPositionDate).ToList();
            }
            else
            {
                if(userId!=null)
                {
                    return DbContext.GpsPosition.Where(p => p.Device.UserId == User.Claims.FirstOrDefault().Value).OrderByDescending(p=>p.GpsPositionDate).ToList();
                }
            }
            return null;
        }

        ///Called by The Internet network
        ///Transfer position of device to db
        /// usage example : host/api/Loc/SaveData (use postman to simulate)
        [HttpPost]
        [Route("/api/[controller]/SaveData")]
        public string SaveData([FromBody]JObject rawPayLoad){
            RawPayLoad loraData = JsonConvert.DeserializeObject<RawPayLoad>(rawPayLoad.ToString());

            //TheThingNetwork send the EUI in the pay_load data
            //EUI is the link between Lora chip and User
            GpsPosition GpsData = new GpsPosition()
            {
                DeviceId = DbContext.Device.Where(p => p.DeviceEUI == loraData.Hardware_serial).Select(p => p.DeviceId).FirstOrDefault(),
                GpsPositionLatitude = DegreeToDecimal(loraData.Payload_fields.Latitude,loraData.Payload_fields.LatitudeDecimal),
                GpsPositionLongitude = DegreeToDecimal(loraData.Payload_fields.Longitude,loraData.Payload_fields.LongitudeDecimal),
                GpsPositionDate = loraData.Metadata.Time,

                //For debugging
                GpsPositionLatitudeRaw = string.Format("{0}.{1}",loraData.Payload_fields.Latitude,loraData.Payload_fields.LatitudeDecimal),
                GpsPositionLongitudeRaw = string.Format("{0}.{1}",loraData.Payload_fields.Longitude,loraData.Payload_fields.LongitudeDecimal)
            };
            DbContext.Add(GpsData);
            DbContext.SaveChanges();
            return "Saved";
        }

        public decimal DegreeToDecimal(int degreeMinute, int decimalMinute)
        {
            //Calculation ex: 5919.12925 -> 59 + 19/60  + 12.925/3600
            //DD = d + (min/60) + (sec/3600)
            int degree = degreeMinute/100;
            decimal minute = (decimal)(degreeMinute % 100)/60;
            decimal second = 0;
            if(decimalMinute>=10000) second = (decimal)(decimalMinute)/1000/3600;
            if(decimalMinute<10000) second = (decimal)(decimalMinute)/100/3600;
            return degree + minute + second;
        }
      
      #endregion
    
        #region methods for APP android or Ios

        ///Check if sensor is moving for APP
        /// usage example : host/api/Loc/IsSensorMoving/deviceEUI/2016-12-01T00:00:00
        [HttpGet]
        [Route("/api/[controller]/GetMotion/{deviceEUI}/{fromThisDate}")]
        public bool GetMotion(string deviceEUI, DateTime fromThisDate)
        {
            Boolean result;
            if(DbContext.Device.Where(p=>p.DeviceEUI == deviceEUI).FirstOrDefault()==null)
                return false;
            
            if (DbContext.GpsPosition.Where(p=>p.Device.DeviceEUI == deviceEUI && DateTime.Compare(p.GpsPositionDate, fromThisDate) >0).Count() >0)
            result =  true;
            else
            result = false;

            return result;
        }

        [HttpGet]
        [Route("/api/[controller]/GetGps/{deviceEUI}")]
        public List<GpsPosition> GetGps(string deviceEUI)
        {
            return DbContext.GpsPosition.Where(p => p.Device.DeviceEUI == deviceEUI).OrderByDescending(p=>p.GpsPositionDate).ToList();
        }

        #endregion
    }
}