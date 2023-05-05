using System.ComponentModel;
using System.Reflection;

namespace WriteFluencyApi.Shared;

public static class EnumExtensions
{
    public static string GetDescription(this Enum value)
    {
        Type type = value.GetType();
        MemberInfo[] memberInfo = type.GetMember(value.ToString());

        if (memberInfo != null && memberInfo.Length > 0)
        {
            object[] attributes = memberInfo[0].GetCustomAttributes(typeof(DescriptionAttribute), false);

            if (attributes != null && attributes.Length > 0)
            {
                return ((DescriptionAttribute)attributes[0]).Description;
            }
        }

        return value.ToString();
    }
}
