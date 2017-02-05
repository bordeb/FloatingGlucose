﻿using Newtonsoft.Json;
using System;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using static FloatingGlucose.Properties.Settings;

namespace FloatingGlucose.Classes.DataSources.Plugins
{
    internal class NightscoutPebbleEndpoint : IDataSourcePlugin
    {
        public virtual bool RequiresBrowseButton => false;
        public virtual string BrowseDialogFileFilter => "";
        public GeneratedNsData NsData;
        public DateTime Date { get; set; }

        public double Glucose { get; set; }
        public double Delta { get; set; }
        public string Direction { get; set; }

        public virtual int SortOrder => 10;

        public double RawDelta => this.RawGlucose - this.PreviousRawGlucose;

        public double RoundedDelta() => Math.Round(this.Delta, 1);

        public double RoundedRawDelta() => Math.Round(this.RawDelta, 1);

        public static CultureInfo Culture = new CultureInfo("en-US");

        public double CalculateRawGlucose(Cal cal, Bg bg, double actualGlucose)
        {
            double number;
            double curBG = actualGlucose;
            int specialValue = 0;

            if (this.IsMmol)
            {
                if ((actualGlucose < 2.2) || (actualGlucose > 22.2))
                {
                    specialValue = 1;
                }

                curBG = curBG * 18.01559;
            }
            else
            {
                if ((actualGlucose < 40) || (actualGlucose > 400))
                {
                    specialValue = 1;
                }
            }

            //this special value is only triggered when the Dexcom upload is brand new
            //from a brand new sensor?
            if (specialValue == 1)
            {
                number = cal.scale * (bg.unfiltered - cal.intercept) / cal.slope;
            }
            else
            {
                number = cal.scale * (bg.filtered - cal.intercept) / cal.slope / curBG;
                number = cal.scale * (bg.unfiltered - cal.intercept) / cal.slope / number;
            }

            if (this.IsMmol)
            {
                number = number / 18.01559;
            }

            return number;
        }

        public double PreviousGlucose
        {
            get
            {
                var bgs = this.NsData.bgs.Skip(1).First();
                return Double.Parse(bgs.sgv, NumberStyles.Any, NightscoutPebbleFileEndpoint.Culture);
            }
        }

        public double PreviousRawGlucose
        {
            get
            {
                try
                {
                    var cal = this.NsData.cals.Skip(1).First();
                    var bg = this.NsData.bgs.Skip(1).First();
                    return this.CalculateRawGlucose(cal, bg, this.PreviousGlucose);
                }
                catch (InvalidOperationException)
                {
                    throw new InvalidJsonDataException("The raw data are not available, enable RAWBG in your azure settings");
                }
            }
        }

        public double RawGlucose
        {
            get
            {
                try
                {
                    var cal = this.NsData.cals.First();
                    var bg = this.NsData.bgs.First();
                    return this.CalculateRawGlucose(cal, bg, this.Glucose);
                }
                catch (InvalidOperationException)
                {
                    throw new InvalidJsonDataException("The raw data are not available, you may have enable RAWBG in your azure settings");
                }
            }
        }

        public DateTime LocalDate => this.Date.ToLocalTime();
        public bool IsMmol => Default.GlucoseUnits == "mmol";
        public virtual string DataSourceShortName => "Nightscout URL";

        public virtual void OnPluginSelected(FormGlucoseSettings form)
        {
            form.lblDataSourceLocation.Text = "Your Nightscout installation URL";
        }

        public virtual bool VerifyConfig(Properties.Settings settings)
        {
            if (!Validators.IsUrl(settings.DataPathLocation) || settings.DataPathLocation == "https://mysite.azurewebsites.net")
            {
                throw new ConfigValidationException("You have entered an invalid Nightscout site URL");
            }

            return true;
        }

        public virtual async Task<IDataSourcePlugin> GetDataSourceDataAsync(NameValueCollection locations)
        {
            var datapath = locations["location"];
            var client = new HttpClient();
            Bg bgs = null;

            string urlContents = await client.GetStringAsync(datapath);

            //urlContents = "{ \"status\":[{\"now\":1471947452808}],\"bgs\":[],\"cals\":[]";
            //urlContents = "{}"
            var parsed =
                this.NsData = JsonConvert.DeserializeObject<GeneratedNsData>(urlContents);
            try
            {
                bgs = parsed.bgs.First();
                this.Direction = bgs.direction;
                this.Glucose = Double.Parse(bgs.sgv, NumberStyles.Any, NightscoutPebbleFileEndpoint.Culture);
                this.Date = DateTimeOffset.FromUnixTimeMilliseconds(bgs.datetime).DateTime;
                this.Delta = Double.Parse(bgs.bgdelta, NumberStyles.Any, NightscoutPebbleFileEndpoint.Culture);
            }
            catch (InvalidOperationException ex)
            {
                //this exception might be hit when the Nightscout installation is brand new or contains no recent data;
                throw new MissingDataException("No data");
            }

            return this;
        }
    }
}