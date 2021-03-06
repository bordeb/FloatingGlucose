﻿using System;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace FloatingGlucose.Classes.Utils
{
    public class DataProtectorConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string);
        }

        public override object ConvertFrom(
    ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
        {
            if (value is string)
            {
                //converts from encrypted text settings into plain text
                var val = (string)value;
                DataProtector protector;
                if (val == null || (val != null && val.Length == 0))
                {
                    protector = new DataProtector("");
                }
                else
                {
                    val = DataProtector.ConvertFromSecureTextString(DataProtector.Base64Decode(val));

                    protector = new DataProtector(val);
                }

                return protector;
            }
            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(
    ITypeDescriptorContext context, System.Globalization.CultureInfo culture,
    object value, Type destinationType)
        {
            if (destinationType == typeof(string))
            {
                // Room room = value as Room;
                // return string.Format("{0},{1}", room.RoomNumber, room.Location);

                var val = (DataProtector)value;
                //encrypts the plaintext and base64 encodes it for storage
                if (val == null || val.Text == null || val.Text.Length == 0)
                {
                    return "";
                }
                else
                {
                    return
                        DataProtector.Base64Encode(DataProtector.ConvertToSecureTextString(val.Text));
                }
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}