/*
  Copyright 2014 Google Inc. All rights reserved.

  Licensed under the Apache License, Version 2.0 (the "License");
  you may not use this file except in compliance with the License.
  You may obtain a copy of the License at

      http://www.apache.org/licenses/LICENSE-2.0

  Unless required by applicable law or agreed to in writing, software
  distributed under the License is distributed on an "AS IS" BASIS,
  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
  See the License for the specific language governing permissions and
  limitations under the License.
*/

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

/*
  GoogleAnalyticsMPV3 handles building hits using the Measurement Protocol.
  Developers should call the methods in GoogleAnalyticsV4, which will call the
  appropriate methods in this class if the application is built for platforms
  other than Android and iOS.
*/
namespace HVR.Editor.Analytics
{
    public class GoogleAnalyticsMPV3
    {
        private string trackingCode;
        private string bundleIdentifier;
        private string appName;
        private string appVersion;
        private bool anonymizeIP;
        private string clientId;
        private string url;
        private Dictionary<Field, object> trackerValues = new Dictionary<Field, object>();
        private bool trackingCodeSet = true;

        public void InitializeTracker()
        {
            if (String.IsNullOrEmpty(trackingCode))
            {
                Debug.Log("No tracking code set for 'Other' platforms - hits will not be set");
                trackingCodeSet = false;
                return;
            }
            clientId = SystemInfo.deviceUniqueIdentifier;
            string language = Application.systemLanguage.ToString();

            #if !UNITY_WP8
            CultureInfo[] cultureInfos = CultureInfo.GetCultures(CultureTypes.AllCultures);
            foreach (CultureInfo info in cultureInfos)
            {
                if (info.EnglishName == Application.systemLanguage.ToString())
                {
                    language = info.Name;
                }
            }
#endif
            url = "https://www.google-analytics.com/collect?v=1"
                + AddRequiredMPParameter(Fields.LANGUAGE, language)
                + AddRequiredMPParameter(Fields.APP_NAME, appName)
                + AddRequiredMPParameter(Fields.TRACKING_ID, trackingCode)
                + AddRequiredMPParameter(Fields.APP_ID, bundleIdentifier)
                + AddRequiredMPParameter(Fields.CLIENT_ID, clientId)
                + AddRequiredMPParameter(Fields.APP_VERSION, appVersion);

            if (anonymizeIP)
            {
                url += AddOptionalMPParameter(Fields.ANONYMIZE_IP, 1);
            }
        }
            

        public void SetTrackerVal(Field field, object value)
        {
            trackerValues[field] = value;
        }

        private string AddTrackerVals()
        {
            if (!trackingCodeSet)
            {
                return "";
            }
            string vals = "";
            foreach (KeyValuePair<Field, object> pair in trackerValues)
            {
                vals += AddOptionalMPParameter(pair.Key, pair.Value);
            }
            return vals;
        }

        private void SendGaHitWithMeasurementProtocol(string url)
        {
            if (String.IsNullOrEmpty(url))
                return;

            // Add random z to avoid caching
            string newUrl = url + "&z=" + UnityEngine.Random.Range(0, 500);

            HVR.Editor.EditorCoroutines.StartCoroutine(this.HandleWWW(new WWW(newUrl)), this);
        }

        /*
          Make request using yield and coroutine to prevent lock up waiting on request to return.
        */
        public IEnumerator HandleWWW(WWW request)
        {
            while (!request.isDone)
            {
                yield return request;
            }
        }

        private string AddRequiredMPParameter(Field parameter, object value)
        {
            if (!trackingCodeSet)
            {
                return "";
            }
            else if (value == null)
            {
                throw new ArgumentNullException();
            }
            else
            {
                return parameter + "=" + WWW.EscapeURL(value.ToString());
            }
        }

        private string AddRequiredMPParameter(Field parameter, string value)
        {
            if (!trackingCodeSet)
            {
                return "";
            }
            else if (value == null)
            {
                throw new ArgumentNullException();
            }
            else
            {
                return parameter + "=" + WWW.EscapeURL(value);
            }
        }

        private string AddOptionalMPParameter(Field parameter, object value)
        {
            if (value == null || !trackingCodeSet)
            {
                return "";
            }
            else
            {
                return parameter + "=" + WWW.EscapeURL(value.ToString());
            }
        }

        private string AddOptionalMPParameter(Field parameter, string value)
        {
            if (String.IsNullOrEmpty(value) || !trackingCodeSet)
            {
                return "";
            }
            else
            {
                return parameter + "=" + WWW.EscapeURL(value);
            }
        }

        private string AddCustomVariables<T>(HitBuilder<T> builder)
        {
            if (!trackingCodeSet)
            {
                return "";
            }
            String url = "";
            foreach (KeyValuePair<int, string> entry in builder.GetCustomDimensions())
            {
                if (entry.Value != null)
                {
                    url += Fields.CUSTOM_DIMENSION.ToString() + entry.Key + "=" +
                        WWW.EscapeURL(entry.Value.ToString());
                }
            }
            foreach (KeyValuePair<int, float> entry in builder.GetCustomMetrics())
            {
                url += Fields.CUSTOM_METRIC.ToString() + entry.Key + "=" +
                    WWW.EscapeURL(entry.Value.ToString());
            }
            return url;
        }


        private string AddCampaignParameters<T>(HitBuilder<T> builder)
        {
            if (!trackingCodeSet)
            {
                return "";
            }
            String url = "";
            url += AddOptionalMPParameter(Fields.CAMPAIGN_NAME, builder.GetCampaignName());
            url += AddOptionalMPParameter(Fields.CAMPAIGN_SOURCE, builder.GetCampaignSource());
            url += AddOptionalMPParameter(Fields.CAMPAIGN_MEDIUM, builder.GetCampaignMedium());
            url += AddOptionalMPParameter(Fields.CAMPAIGN_KEYWORD, builder.GetCampaignKeyword());
            url += AddOptionalMPParameter(Fields.CAMPAIGN_CONTENT, builder.GetCampaignContent());
            url += AddOptionalMPParameter(Fields.CAMPAIGN_ID, builder.GetCampaignID());
            url += AddOptionalMPParameter(Fields.GCLID, builder.GetGclid());
            url += AddOptionalMPParameter(Fields.DCLID, builder.GetDclid());

            return url;
        }

        public void LogEvent(EventHitBuilder builder)
        {
            trackerValues[Fields.EVENT_CATEGORY] = null;
            trackerValues[Fields.EVENT_ACTION] = null;
            trackerValues[Fields.EVENT_LABEL] = null;
            trackerValues[Fields.EVENT_VALUE] = null;

            SendGaHitWithMeasurementProtocol(url
                + AddRequiredMPParameter(Fields.HIT_TYPE, "event")
                + AddOptionalMPParameter(Fields.EVENT_CATEGORY, builder.GetEventCategory())
                + AddOptionalMPParameter(Fields.EVENT_ACTION, builder.GetEventAction())
                + AddOptionalMPParameter(Fields.EVENT_LABEL, builder.GetEventLabel())
                + AddOptionalMPParameter(Fields.EVENT_VALUE, builder.GetEventValue())
                + AddCustomVariables(builder)
                + AddCampaignParameters(builder)
                + AddTrackerVals());
        }

        public void ClearUserIDOverride()
        {
            SetTrackerVal(Fields.USER_ID, null);
        }

        public void SetTrackingCode(string trackingCode)
        {
            this.trackingCode = trackingCode;
        }

        public void SetBundleIdentifier(string bundleIdentifier)
        {
            this.bundleIdentifier = bundleIdentifier;
        }

        public void SetAppName(string appName)
        {
            this.appName = appName;
        }

        public void SetAppVersion(string appVersion)
        {
            this.appVersion = appVersion;
        }

        public void SetAnonymizeIP(bool anonymizeIP)
        {
            this.anonymizeIP = anonymizeIP;
        }
    }
}
