﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using TeaCommerce.Api.Models;
using TeaCommerce.Api.PaymentProviders;
using TeaCommerce.PaymentProviders.Extensions;
using TeaCommerce.Api.Infrastructure.Logging;

namespace TeaCommerce.PaymentProviders {

  [PaymentProvider( "2CheckOut" )]
  public class TwoCheckOut : APaymentProvider {

    protected const string defaultParameterValue = "";

    public override bool SupportsRetrievalOfPaymentStatus { get { return false; } }
    public override bool SupportsCancellationOfPayment { get { return false; } }
    public override bool SupportsCapturingOfPayment { get { return false; } }
    public override bool SupportsRefundOfPayment { get { return false; } }

    public override IDictionary<string, string> DefaultSettings {
      get {
        if ( defaultSettings == null ) {
          defaultSettings = new Dictionary<string, string>();
          defaultSettings[ "sid" ] = string.Empty;
          defaultSettings[ "lang" ] = "en";
          defaultSettings[ "x_receipt_link_url" ] = string.Empty;
          defaultSettings[ "secretWord" ] = string.Empty;
          defaultSettings[ "productNumberPropertyAlias" ] = "productNumber";
          defaultSettings[ "productNamePropertyAlias" ] = "productName";
          defaultSettings[ "shippingMethodProductNumber" ] = "1000";
          defaultSettings[ "shippingMethodFormatString" ] = "Shipping fee ({0})";
          defaultSettings[ "paymentMethodProductNumber" ] = "2000";
          defaultSettings[ "paymentMethodFormatString" ] = "Payment fee ({0})";
          defaultSettings[ "streetAddressPropertyAlias" ] = "streetAddress"; //TODO: hedder de også det i den nye best practice??
          defaultSettings[ "cityPropertyAlias" ] = "city";
          defaultSettings[ "zipCodePropertyAlias" ] = "zipCode";
          defaultSettings[ "phonePropertyAlias" ] = "phone";
          defaultSettings[ "phoneExtensionPropertyAlias" ] = "phoneExtension";
          defaultSettings[ "demo" ] = "N";
        }
        return defaultSettings;
      }
    }

    public override string FormPostUrl { get { return "https://www.2checkout.com/checkout/spurchase"; } }
    public override string DocumentationLink { get { return "http://anders.burla.dk/umbraco/tea-commerce/using-2checkout-with-tea-commerce/"; } }
    public override bool FinalizeAtContinueUrl { get { return true; } }

    public override IDictionary<string, string> GenerateForm( Order order, string teaCommerceContinueUrl, string teaCommerceCancelUrl, string teaCommerceCallBackUrl, IDictionary<string, string> settings ) {
      List<string> settingsToExclude = new string[] { "secretWord", "productNumberPropertyAlias", "productNamePropertyAlias", "shippingMethodProductNumber", "shippingMethodFormatString", "paymentMethodProductNumber", "paymentMethodFormatString", "streetAddressPropertyAlias", "cityPropertyAlias", "statePropertyAlias", "zipCodePropertyAlias", "phonePropertyAlias", "phoneExtensionPropertyAlias" }.ToList();
      Dictionary<string, string> inputFields = settings.Where( i => !settingsToExclude.Contains( i.Key ) ).ToDictionary( i => i.Key, i => i.Value );

      //cartId
      inputFields[ "cart_order_id" ] = order.CartNumber;

      //amount
      string amount = order.TotalPrice.WithVat.ToString( "0.00", CultureInfo.InvariantCulture );
      inputFields[ "total" ] = amount;

      inputFields[ "x_receipt_link_url" ] = teaCommerceContinueUrl;

      //card_holder_name
      inputFields[ "card_holder_name" ] = order.PaymentInformation.FirstName + " " + order.PaymentInformation.LastName;

      //street_address
      CustomProperty streetAddressProperty = settings.ContainsKey( "streetAddressPropertyAlias" ) ? order.Properties.Get( settings[ "streetAddressPropertyAlias" ] ) : null;
      inputFields[ "street_address" ] = streetAddressProperty != null && !string.IsNullOrEmpty( streetAddressProperty.Value ) ? streetAddressProperty.Value : defaultParameterValue;

      //city
      CustomProperty cityPropertyAlias = settings.ContainsKey( "cityPropertyAlias" ) ? order.Properties.Get( settings[ "cityPropertyAlias" ] ) : null;
      inputFields[ "city" ] = cityPropertyAlias != null && !string.IsNullOrEmpty( cityPropertyAlias.Value ) ? cityPropertyAlias.Value : defaultParameterValue;

      //state
      //TODO - se på states i alle providers
      CustomProperty statePropertyAlias = settings.ContainsKey( "statePropertyAlias" ) ? order.Properties.Get( settings[ "statePropertyAlias" ] ) : null;
      inputFields[ "state" ] = statePropertyAlias != null && !string.IsNullOrEmpty( statePropertyAlias.Value ) ? statePropertyAlias.Value : defaultParameterValue;

      //zip
      CustomProperty zipCodePropertyAlias = settings.ContainsKey( "zipCodePropertyAlias" ) ? order.Properties.Get( settings[ "zipCodePropertyAlias" ] ) : null;
      inputFields[ "zip" ] = zipCodePropertyAlias != null && !string.IsNullOrEmpty( zipCodePropertyAlias.Value ) ? zipCodePropertyAlias.Value : defaultParameterValue;

      //country
      inputFields[ "country" ] = order.Country.CountryCode;

      //email
      inputFields[ "email" ] = order.PaymentInformation.Email;

      //phone
      CustomProperty phonePropertyAlias = settings.ContainsKey( "phonePropertyAlias" ) ? order.Properties.Get( settings[ "phonePropertyAlias" ] ) : null;
      inputFields[ "phone" ] = phonePropertyAlias != null && !string.IsNullOrEmpty( phonePropertyAlias.Value ) ? phonePropertyAlias.Value : defaultParameterValue;

      //phone_extension
      CustomProperty phoneExtensionPropertyAlias = settings.ContainsKey( "phoneExtensionPropertyAlias" ) ? order.Properties.Get( settings[ "phoneExtensionPropertyAlias" ] ) : null;
      inputFields[ "phone_extension" ] = phoneExtensionPropertyAlias != null && !string.IsNullOrEmpty( phoneExtensionPropertyAlias.Value ) ? phoneExtensionPropertyAlias.Value : defaultParameterValue;

      //fixed
      inputFields[ "fixed" ] = "Y";

      //skip_landing
      inputFields[ "skip_landing" ] = "1";

      //Testing
      if ( inputFields.ContainsKey( "demo" ) && inputFields[ "demo" ] != "Y" )
        inputFields.Remove( "demo" );

      //fixed
      inputFields[ "id_type" ] = "1";

      int itemIndex = 1;
      //Lines are added in reverse order of the UI

      //Payment fee
      if ( order.PaymentInformation.TotalPrice.WithVat != 0 ) {
        inputFields[ "c_prod_" + itemIndex ] = settings[ "paymentMethodProductNumber" ] + ",1";
        inputFields[ "c_name_" + itemIndex ] = string.Format( settings[ "paymentMethodFormatString" ], order.PaymentMethod.Name ).Truncate( 128 );
        inputFields[ "c_description_" + itemIndex ] = string.Empty;
        inputFields[ "c_price_" + itemIndex ] = order.PaymentInformation.TotalPrice.WithVat.ToString( "0.00", CultureInfo.InvariantCulture );
        itemIndex++;
      }

      //Shipping fee
      if ( order.ShipmentInformation.TotalPrice.WithVat != 0 ) {
        inputFields[ "c_prod_" + itemIndex ] = settings[ "shippingMethodProductNumber" ] + ",1";
        inputFields[ "c_name_" + itemIndex ] = string.Format( settings[ "shippingMethodFormatString" ], order.ShippingMethod.Name ).Truncate( 128 );
        inputFields[ "c_description_" + itemIndex ] = string.Empty;
        inputFields[ "c_price_" + itemIndex ] = order.ShipmentInformation.TotalPrice.WithVat.ToString( "0.00", CultureInfo.InvariantCulture );
        itemIndex++;
      }

      //Order line information
      List<OrderLine> orderLines = order.OrderLines.ToList();
      OrderLine orderLine;

      for ( int i = orderLines.Count - 1; i >= 0; i-- ) {
        orderLine = orderLines[ i ];
        CustomProperty productNameProp = orderLine.Properties.Get( settings[ "productNamePropertyAlias" ] );
        CustomProperty productNumberProp = orderLine.Properties.Get( settings[ "productNumberPropertyAlias" ] );

        inputFields[ "c_prod_" + itemIndex ] = productNumberProp.Value + "," + orderLine.Quantity.ToString();
        inputFields[ "c_name_" + itemIndex ] = productNameProp.Value.Truncate( 128 );
        inputFields[ "c_description_" + itemIndex ] = string.Empty;
        inputFields[ "c_price_" + itemIndex ] = orderLine.UnitPrice.WithVat.ToString( "0.00", CultureInfo.InvariantCulture );

        itemIndex++;
      }

      return inputFields;
    }

    public override string GetContinueUrl( IDictionary<string, string> settings ) {
      return settings[ "x_receipt_link_url" ];
    }

    public override string GetCancelUrl( IDictionary<string, string> settings ) {
      return string.Empty;
    }

    public override CallbackInfo ProcessCallback( Order order, HttpRequest request, IDictionary<string, string> settings ) {
      //using ( StreamWriter writer = new StreamWriter( File.Create( HttpContext.Current.Server.MapPath( "~/2CheckOutTestCallback.txt" ) ) ) ) {
      //  writer.WriteLine( "QueryString:" );
      //  foreach ( string k in request.QueryString.Keys ) {
      //    writer.WriteLine( k + " : " + request.QueryString[ k ] );
      //  }
      //  writer.Flush();
      //}

      string errorMessage = string.Empty;

      string accountNumber = request.QueryString[ "sid" ];
      string transaction = request.QueryString[ "order_number" ];
      string strAmount = request.QueryString[ "total" ];
      string key = request.QueryString[ "key" ];

      string md5CheckValue = string.Empty;
      md5CheckValue += settings[ "secretWord" ];
      md5CheckValue += accountNumber;
      md5CheckValue += settings[ "demo" ] != "Y" ? transaction : "1";
      md5CheckValue += strAmount;

      string calculatedMD5 = GetMD5Hash( md5CheckValue ).ToUpperInvariant();

      if ( calculatedMD5 == key ) {
        decimal totalAmount = decimal.Parse( strAmount, CultureInfo.InvariantCulture );
        PaymentState paymentState = PaymentState.Authorized;

        return new CallbackInfo( totalAmount, transaction, paymentState );
      } else
        errorMessage = "Tea Commerce - 2CheckOut - MD5Sum security check failed - key: " + key + " - calculatedMD5: " + calculatedMD5;

      LoggingService.Instance.Log( errorMessage );
      return new CallbackInfo( errorMessage );
    }

    public override ApiInfo GetStatus( Order order, IDictionary<string, string> settings ) {
      throw new NotImplementedException();
    }

    public override ApiInfo CapturePayment( Order order, IDictionary<string, string> settings ) {
      throw new NotImplementedException();
    }

    public override ApiInfo RefundPayment( Order order, IDictionary<string, string> settings ) {
      throw new NotImplementedException();
    }

    public override ApiInfo CancelPayment( Order order, IDictionary<string, string> settings ) {
      throw new NotImplementedException();
    }

    public override string GetLocalizedSettingsKey( string settingsKey, CultureInfo culture ) {
      switch ( settingsKey ) {
        case "x_receipt_link_url":
          return settingsKey + "<br/><small>e.g. /continue/</small>";
        case "demo":
          return settingsKey + "<br/><small>Y = true; N = false</small>";
        default:
          return base.GetLocalizedSettingsKey( settingsKey, culture );
      }
    }

  }
}
