using System;
using System.Configuration;
using System.Threading.Tasks;
using System.Web.Mvc;
using Fitbit.Api.Portable;
using Fitbit.Api.Portable.Models;
using Fitbit.Api.Portable.OAuth2;
using Fitbit.Models;

namespace SampleWebMVC.Portable.Controllers
{
    public class FitbitController : Controller
    {
        // GET: Fitbit
        public ActionResult Index()
        {
            return RedirectToAction("Index", "Home");
        }

        //
        // GET: /Authorize/
        // Setup - prepare the user redirect to Fitbit.com to prompt them to authorize this app.
        public async Task<ActionResult> Authorize()
        {
            //make sure you've set these up in Web.Config under <appSettings>:
            var appCredentials = new FitbitAppCredentials()
            {
                ClientId = ConfigurationManager.AppSettings["FitbitConsumerKey"],
                ClientSecret = ConfigurationManager.AppSettings["FitbitConsumerSecret"]
            };

            //Provide the App Credentials. You get those by registering your app at dev.fitbit.com
            //Configure Fitbit authenticaiton request to perform a callback to this constructor's Callback method
            var authenticator = new OAuth2Helper(appCredentials, Request.Url?.GetLeftPart(UriPartial.Authority) + "/Fitbit/Callback");
            string[] scopes = new string[] { "profile" };

            Session["FitbitAuthenticator"] = authenticator;

            //note: at this point the RequestToken object only has the Token and Secret properties supplied. Verifier happens later.
            string authUrl = authenticator.GenerateAuthUrl(scopes);

            return Redirect(authUrl);
        }

        //Final step. Take this authorization information and use it in the app
        public async Task<ActionResult> Callback(string code)
        {
            RequestToken token = new RequestToken();
            token.Token = Request.Params["oauth_token"];
            token.Secret = Session["FitbitRequestTokenSecret"].ToString();
            token.Verifier = Request.Params["oauth_verifier"];

            //this is going to go back to Fitbit one last time (server to server) and get the user's permanent auth credentials

            //create the Authenticator object
            var authenticator = (OAuth2Helper)Session["FitbitAuthenticator"];

            //execute the Authenticator request to Fitbit
            OAuth2AccessToken tocken = await authenticator.ExchangeAuthCodeForAccessTokenAsync(code);

            //here, we now have everything we need for the future to go back to Fitbit's API (STORE THESE):
            //  credential.AuthToken;
            //  credential.AuthTokenSecret;
            //  credential.UserId;
            
            return RedirectToAction("Index", "Home");
        }

        public async Task<ActionResult> Devices()
        {
            var client = GetFitbitClient();
            var response = await client.GetDevicesAsync();
            return View(response);
        }

        public async Task<ActionResult> Friends()
        {
            var client = GetFitbitClient();
            var response = await client.GetFriendsAsync();
            return View(response);
        }

        public async Task<ActionResult> UserProfile()
        {
            var client = GetFitbitClient();
            var response = await client.GetUserProfileAsync();
            return View(response);
        }

        public async Task<ActionResult> LastWeekDistance()
        {
            var client = GetFitbitClient();
            var response = await client.GetTimeSeriesAsync(TimeSeriesResourceType.Distance, DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);
            return View($"TimeSeriesDataList", response);
        }

        public async Task<ActionResult> LastWeekSteps()
        {
            var client = GetFitbitClient();
            var response = await client.GetTimeSeriesIntAsync(TimeSeriesResourceType.Steps, DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);
            return View($"TimeSeriesDataList", response);
        }

        /// <summary>
        /// HttpClient and hence FitbitClient are designed to be long-lived for the duration of the session. This method ensures only one client is created for the duration of the session.
        /// More info at: http://stackoverflow.com/questions/22560971/what-is-the-overhead-of-creating-a-new-httpclient-per-call-in-a-webapi-client
        /// </summary>
        /// <returns></returns>
        private FitbitClient GetFitbitClient(OAuth2AccessToken accessToken = null)
        {
            if (Session["FitbitClient"] == null)
            {
                if (accessToken != null)
                {
                    var appCredentials = (FitbitAppCredentials)Session["AppCredentials"];
                    FitbitClient client = new FitbitClient(appCredentials, accessToken);
                    Session["FitbitClient"] = client;
                    return client;
                }
                else
                {
                    throw new Exception("First time requesting a FitbitClient from the session you must pass the AccessToken.");
                }

            }
            else
            {
                return (FitbitClient)Session["FitbitClient"];
            }
        }
    }
}