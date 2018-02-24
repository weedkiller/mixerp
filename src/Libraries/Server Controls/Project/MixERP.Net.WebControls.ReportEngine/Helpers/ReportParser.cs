﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Hosting;
using MixERP.Net.ApplicationState.Cache;
using MixERP.Net.Common;
using MixERP.Net.Common.Helpers;
using MixERP.Net.Framework;
using MixERP.Net.Framework.Extensions;
using MixERP.Net.i18n;

namespace MixERP.Net.WebControls.ReportEngine.Helpers
{
    public static class ReportParser
    {
        public static string ParseDataSource(string expression, Collection<DataTable> table)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return null;
            }

            if (table == null)
            {
                return null;
            }

            foreach (var match in Regex.Matches(expression, "{.*?}"))
            {
                string word = match.ToString();

                if (word.StartsWith("{DataSource", StringComparison.OrdinalIgnoreCase))
                {
                    int index =
                        Conversion.TryCastInteger(word.Split('.').First().Replace("{DataSource[", "").Replace("]", ""));
                    string column = word.Split('.').Last().Replace("}", "");

                    if (table[index] != null)
                    {
                        if (table[index].Rows.Count > 0)
                        {
                            if (table[index].Columns.Contains(column))
                            {
                                object columnValue = table[index].Rows[0][column];

                                string value;

                                if (columnValue is decimal)
                                {
                                    value = columnValue.To<decimal>().ToString("N2");
                                }
                                else
                                {
                                    value = Regex.Replace(columnValue.ToString(), @"\r\n?|\n", "<br />", RegexOptions.Compiled);
                                }

                                expression = expression.Replace(word, value);
                            }
                        }
                    }
                }
            }

            return expression;
        }

        private static string GetDictionaryValue(string key)
        {
            string globalLoginId = HttpContext.Current.User.Identity.Name;

            if (!string.IsNullOrWhiteSpace(globalLoginId))
            {
                string cacheKey = "Dictionary" + globalLoginId;

                Dictionary<string, object> dictionary =
                    CacheFactory.GetFromDefaultCacheByKey(cacheKey) as Dictionary<string, object>;

                if (dictionary != null)
                {
                    object value = dictionary[key];

                    if (value != null)
                    {
                        return Conversion.TryCastString(value);
                    }
                }
            }

            return string.Empty;
        }

        private static string GetLogo()
        {
            //Default logo
            string logo = ConfigurationHelper.GetReportParameter("LogoPath");


            string officeLogoFile = AppUsers.GetCurrent().View.LogoFile;

            if (!string.IsNullOrWhiteSpace(officeLogoFile))
            {
                string attachmentsDirectory = DbConfig.GetAttachmentParameter(AppUsers.GetCurrentUserDB(),
                    "AttachmentsDirectory");

                officeLogoFile = Path.Combine(attachmentsDirectory, officeLogoFile);

                if (File.Exists(HostingEnvironment.MapPath(officeLogoFile)))
                {
                    logo = officeLogoFile;
                }
            }

            return logo;
        }

        public static string ParseExpression(string expression, Collection<DataTable> dataTableCollection)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return string.Empty;
            }


            string logo = GetLogo();
            expression = expression.Replace("{LogoPath}",
                PageUtility.GetCurrentDomainName() + PageUtility.ResolveUrl(logo));
                //Or else logo will not be exported into excel.
            expression = expression.Replace("{PrintDate}", DateTime.Now.ToString(CultureManager.GetCurrentUICulture()));

            foreach (var match in Regex.Matches(expression, "{.*?}"))
            {
                string word = match.ToString();

                if (word.StartsWith("{CurrentOffice.", StringComparison.OrdinalIgnoreCase))
                {
                    string sessionKey = RemoveBraces(word);
                    sessionKey = sessionKey.Replace("CurrentOffice.", "");
                    sessionKey = sessionKey.Trim();

                    string value = GetDictionaryValue(sessionKey);

                    expression = expression.Replace(word, value);
                }
                else if (word.StartsWith("{Resources.", StringComparison.OrdinalIgnoreCase))
                {
                    string res = RemoveBraces(word);
                    string[] resource = res.Split('.');

                    string key = resource[2];

                    expression = expression.Replace(word, ResourceManager.GetString(resource[1], key));
                }
                else if (word.StartsWith("{DataSource", StringComparison.OrdinalIgnoreCase) &&
                         word.ToLower(CultureInfo.InvariantCulture).Contains("runningtotalfieldvalue"))
                {
                    string res = RemoveBraces(word);
                    string[] resource = res.Split('.');

                    int dataSourceIndex =
                        Conversion.TryCastInteger(
                            resource[0].ToLower(CultureInfo.InvariantCulture)
                                .Replace("datasource", "")
                                .Replace("[", "")
                                .Replace("]", ""));
                    int index =
                        Conversion.TryCastInteger(
                            resource[1].ToLower(CultureInfo.InvariantCulture)
                                .Replace("runningtotalfieldvalue", "")
                                .Replace("[", "")
                                .Replace("]", ""));

                    if (dataSourceIndex >= 0 && index >= 0)
                    {
                        if (dataTableCollection != null && dataTableCollection[dataSourceIndex] != null)
                        {
                            expression = expression.Replace(word,
                                GetSum(dataTableCollection[dataSourceIndex], index)
                                    .ToString("N2"));
                        }
                    }
                }
                else if (word.StartsWith("{Barcode", StringComparison.OrdinalIgnoreCase))
                {
                    string res = RemoveBraces(word).Replace("Barcode(", "").Replace(")", "");
                    string barCodeValue = res;

                    if (res.StartsWith("DataSource"))
                    {
                        barCodeValue = ParseDataSource("{" + res + "}", dataTableCollection);
                    }

                    string barCodeFormat = ConfigurationHelper.GetReportParameter("BarCodeFormat");
                    string barCodeDisplayValue = ConfigurationHelper.GetReportParameter("BarCodeDisplayValue");
                    string barCodeFontSize = ConfigurationHelper.GetReportParameter("BarCodeFontSize");
                    string barCodeWidth = ConfigurationHelper.GetReportParameter("BarCodeWidth");
                    string barCodeHeight = ConfigurationHelper.GetReportParameter("BarCodeHeight");
                    string barCodeQuite = ConfigurationHelper.GetReportParameter("BarCodeQuite");
                    string barCodeFont = ConfigurationHelper.GetReportParameter("BarCodeFont");
                    string barCodeTextAlign = ConfigurationHelper.GetReportParameter("BarCodeTextAlign");
                    string barCodeBackgroundColor = ConfigurationHelper.GetReportParameter("BarCodeBackgroundColor");
                    string barCodeLineColor = ConfigurationHelper.GetReportParameter("BarCodeLineColor");

                    string imageSource =
                        "<img class='reportEngineBarCode' data-barcodevalue='{0}' alt='{0}' value='{0}' data-barcodeformat='{1}' data-barcodedisplayvalue='{2}' data-barcodefontsize='{3}' data-barcodewidth='{4}' data-barcodeheight='{5}' data-barcodefont='{6}' data-barcodetextalign='{7}' data-barcodebackgroundcolor='{8}' data-barcodelinecolor='{9}' data-barcodequite={10} />";
                    imageSource = string.Format(CultureInfo.InvariantCulture, imageSource, barCodeValue,
                        barCodeFormat, barCodeDisplayValue, barCodeFontSize, barCodeWidth, barCodeHeight,
                        barCodeFont, barCodeTextAlign, barCodeBackgroundColor, barCodeLineColor, barCodeQuite);
                    expression = expression.Replace(word, imageSource).ToString(CultureInfo.InvariantCulture);
                }
                else if (word.StartsWith("{QRCode", StringComparison.OrdinalIgnoreCase))
                {
                    string res = RemoveBraces(word).Replace("QRCode(", "").Replace(")", "");
                    string qrCodeValue = res;

                    if (res.StartsWith("DataSource"))
                    {
                        qrCodeValue = ParseDataSource("{" + res + "}", dataTableCollection);
                    }

                    string qrCodeRender = ConfigurationHelper.GetReportParameter("QRCodeRender");
                    string qrCodeBackgroundColor = ConfigurationHelper.GetReportParameter("QRCodeBackgroundColor");
                    string qrCodeForegroundColor = ConfigurationHelper.GetReportParameter("QRCodeForegroundColor");
                    string qrCodeWidth = ConfigurationHelper.GetReportParameter("QRCodeWidth");
                    string qrCodeHeight = ConfigurationHelper.GetReportParameter("QRCodeHeight");
                    string qrCodeTypeNumber = ConfigurationHelper.GetReportParameter("QRCodeTypeNumber");

                    string qrCodeDiv =
                        "<div class='reportEngineQRCode' data-qrcodevalue={0} data-qrcoderender='{1}' data-qrcodebackgroundcolor='{2}' data-qrcodeforegroundcolor='{3}' data-qrcodewidth='{4}' data-qrcodeheight='{5}' data-qrcodetypenumber='{6}'></div>";
                    qrCodeDiv = string.Format(CultureInfo.InvariantCulture, qrCodeDiv, qrCodeValue, qrCodeRender,
                        qrCodeBackgroundColor, qrCodeForegroundColor, qrCodeWidth, qrCodeHeight, qrCodeTypeNumber);
                    expression = expression.Replace(word, qrCodeDiv).ToString(CultureInfo.InvariantCulture);
                }
            }

            return expression;
        }

        public static string RemoveBraces(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return string.Empty;
            }

            return expression.Replace("{", "").Replace("}", "");
        }

        private static decimal GetSum(DataTable table, int index)
        {
            if (table != null && table.Rows.Count > 0)
            {
                string expression = "SUM(" + table.Columns[index].ColumnName + ")";
                return Conversion.TryCastDecimal(table.Compute(expression, ""));
            }

            return 0;
        }
    }
}