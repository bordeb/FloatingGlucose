﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace DexcomShareNet
{
    internal class ShareClient
    {
        private string dexcomUserAgent = "Dexcom Share/3.0.2.11 CFNetwork/711.2.23 Darwin/14.0.0";
        private string dexcomApplicationId = "d89443d2-327c-4a6f-89e5-496bbb0317db";
        private string dexcomLoginPath = "/ShareWebServices/Services/General/LoginPublisherAccountByName";
        private string dexcomLatestGlucosePath = "/ShareWebServices/Services/Publisher/ReadPublisherLatestGlucoseValues";

        private string dexcomServerUS = "https://share1.dexcom.com";
        //private string dexcomServerUS = "https://example.com";

        private string dexcomServerNonUS = "https://shareous1.dexcom.com";

        private string dexcomServer;

        private int maxReauthAttempts = 3;

        private string username;
        private string password;

        private string token;

        protected bool enableDebug = true;

        protected virtual void WriteDebug(string msg)
        {
            if (!this.enableDebug)
            {
                return;
            }
            Console.WriteLine(nameof(ShareClient) + ": " + msg);
        }

        private async Task<ShareResponse> dexcomPOST(string url, Dictionary<string, string> data = null)
        {
            return await this.dexcomPOST(new Uri(url), data);
        }

        private async Task<ShareResponse> dexcomPOST(Uri url, Dictionary<string, string> data = null)
        {
            var json = JsonConvert.SerializeObject(data);
            var client = new HttpClient();

            var msg = new HttpRequestMessage(new HttpMethod("POST"), url);

            msg.Headers.Accept.Clear();
            msg.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            msg.Headers.UserAgent.Clear();
            msg.Headers.Add("user-agent", this.dexcomUserAgent);



            msg.Content = new StringContent(json, Encoding.UTF8, "application/json");


            try
            {
                WriteDebug($"Sending json {json} to endpoint {url}");
                var response = (await client.SendAsync(msg));
                WriteDebug($"Is response success? {response.IsSuccessStatusCode}");
                var result = await response.Content.ReadAsStringAsync();

                WriteDebug($"Response from endpoint {url}: {result}");
                return new ShareResponse { IsSuccess = response.IsSuccessStatusCode, Response = result };

            }
            catch (Exception err)
            {
                WriteDebug($"Got exception in sending to endpoint {err}");
            }

            return null;
        }

        public ShareClient(string username, string password, ShareServer shareServer = ShareServer.ShareServerUS)
        {
            this.username = username;
            this.password = password;

            this.dexcomServer = shareServer == ShareServer.ShareServerUS ?
                this.dexcomServerUS :
                this.dexcomServerNonUS;
        }

        public async Task<string> fetchToken()
        {
            string decoded = null;
            var data = new Dictionary<string, string>()
            {
                { "accountName" , this.username},
                { "password" , this.password},
                { "applicationId" , this.dexcomApplicationId},
            };

            var url = this.dexcomServer + this.dexcomLoginPath;

            WriteDebug($"Post to {url}");

            var result = (await this.dexcomPOST(url, data)).GetResponse();
            decoded = JsonConvert.DeserializeObject<string>(result);

            return decoded;
        }

        public async Task<List<ShareGlucose>> FetchLast(int n)
        {
            return await this.fetchLastGlucoseValuesWithRetries(n, this.maxReauthAttempts);
        }

        //should be private after test
        private async Task<List<ShareGlucose>> fetchLastGlucoseValuesWithRetries(int n = 3, int remaining = 3)
        {
            List<ShareGlucose> result = null;
            var i = 0;
            do
            {
                //logic for refetching token/reauth here, but missing currently
                try
                {
                    i++;
                    WriteDebug($"Attempt #{i} to fetch glucose");
                    result = await this.fetchLastGlucoseValues(n);
                }
                catch (WebException)
                {
                    //ignore webexceptions, might mean network is temporarily down, retry
                    WriteDebug("Got webexception");
                }
                catch (HttpRequestException)
                {
                    //ignore webexceptions, might mean network is temporarily down, retry
                    WriteDebug("Got httprequestexception");
                }
                catch (SpecificShareError err)
                {
                    if (err.code == "SessionIdNotFound" || err.code == "SessionNotValid")
                    {
                        // Token is invalid, force trying to fetching new token on next call
                        // to FetchLastGlucoseValues
                        this.token = null;
                        WriteDebug("Session not found, must reauth");
                    }
                    else
                    {
                        //rethrow because we don't know how to handle other errors
                        throw err;
                    }
                }
            } while (result == null && remaining-- > 0);

            return result;
        }

        //should be private after testubg
        private async Task<List<ShareGlucose>> fetchLastGlucoseValues(int n = 3)
        {
            if (this.token == null)
            {
                WriteDebug("Fetching token from inside FetchLastGlucoseValues");
                this.token = await this.fetchToken();
            }

            //
            // We failed to retrieve token, retry will be handled by FetchLastGlucoseValuesWithRetries
            //
            if (this.token == null)
            {
                return null;
            }

            var url = $"{this.dexcomServer}{this.dexcomLatestGlucosePath}?sessionId={this.token}&minutes=1440&maxCount={n}";

            var response = (await this.dexcomPOST(url)).GetResponse();

            var parsed = JsonConvert.DeserializeObject<List<ShareGlucose>>(response);

            return parsed;
        }
    }
}