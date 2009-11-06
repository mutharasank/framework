﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Resources;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using Signum.Utilities;
using System.Reflection;
using Signum.Utilities.Reflection;
using Signum.Entities.Reflection;

namespace Signum.Entities
{
    [AttributeUsage(AttributeTargets.Class)]
    public class PluralLocDescriptionAttribute : DescriptionAttribute
    {
        public bool Auto { get { return resourceKey == null || resourceSource == null; } }

        Type resourceSource;
        string resourceKey;

        public PluralLocDescriptionAttribute()
        {
        }

        public PluralLocDescriptionAttribute(Type resourceSource, string resourceKey)
            : base()
        {
            this.resourceSource = resourceSource;
            this.resourceKey = resourceKey;
        }

        public override string Description
        {
            get
            {
                if (Auto)
                    throw new ApplicationException("Use ReflectionTools.GetPluralDescription instead");

                return new ResourceManager(resourceSource).GetString(resourceKey);
            }
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class PluralDescriptionAttribute : DescriptionAttribute
    {
        public PluralDescriptionAttribute(string description) : base(description) { }
    }

    public static class Pluralizer
    {
        public static Dictionary<string, IPluralizer> Pluralizers = new Dictionary<string, IPluralizer>
        {
            {"es", new SpanishPluralizer()},
            {"en", new EnglishPluralizer()},
        };

        public static string GetPluralDescription(Type type)
        {
            PluralLocDescriptionAttribute loc = type.SingleAttribute<PluralLocDescriptionAttribute>();
            if (loc != null)
            {
                if (loc.Auto)
                {
                    string key = type.Name + "_Plural";
                    Assembly assembly = type.Assembly;
                    return assembly.GetDefaultResourceManager().GetString(key, CultureInfo.CurrentCulture);
                }
                else
                    return loc.Description;
            }

            PluralDescriptionAttribute desc = type.SingleAttribute<PluralDescriptionAttribute>();
            if (desc != null)
            {
                return desc.Description;
            }

            return null;
        }

        internal static string Pluralize(string singularName)
        {
            IPluralizer pluralizer = Pluralizers.TryGetC(CultureInfo.CurrentCulture.TwoLetterISOLanguageName);
            if (pluralizer != null)
                return pluralizer.MakePlural(singularName);

            return singularName;
        }
    }

    public interface IPluralizer
    {
        string MakePlural(string singularName);
    }

    public class EnglishPluralizer:IPluralizer
    {
        //http://www.csse.monash.edu.au/~damian/papers/HTML/Plurals.html
        Dictionary<string, string> exceptions = new Dictionary<string,string>
        {
            {"an", "en"}, // woman -> women 
            {"ch", "ches"}, // church -> churches 
            {"eau", "eaus"},  //chateau -> chateaus
            {"en", "ens"}, //foramen -> foramens
            {"ex", "exes"}, //index -> indexes
            {"f", "ves"}, //wolf -> wolves 
            {"fe", "ves"}, //wolf -> wolves 
            {"ieu", "ieus milieu"}, //milieu-> mileus
            {"is", "is"}, //basis -> basis 
            {"ix", "ixes"}, //matrix -> matrixes
            {"nx", "nxes"}, //phalanx -> phalanxes 
            {"s", "s"}, //series -> series 
            {"sh", "shes"}, //wish -> wishes 
            {"us",  "us"},// genus -> us 
            {"x",  "xes"},// box -> boxes 
            {"y", "ies"}, //ferry -> ferries 
        }; 

        public string MakePlural(string singularName)
        {
            if(string.IsNullOrEmpty(singularName))
                return singularName;


            int index = singularName.IndexOf(' '); 

            if(index != -1)
                return MakePlural(singularName.Substring(0, index)) + singularName.Substring(index);

            var result = exceptions.FirstOrDefault(r=>singularName.EndsWith(r.Key)); 
            if(result.Value != null)
                return singularName.Substring(0, singularName.Length - result.Key.Length) + result.Value;

            return singularName + "s";
        }
    }

    public class SpanishPluralizer:IPluralizer
    {
        //http://es.wikipedia.org/wiki/Formaci%C3%B3n_del_plural_en_espa%C3%B1ol
        Dictionary<string, string> exceptions = new Dictionary<string, string>
        {
            {"x", ""}, // tórax -> tórax
            {"s", ""}, // church -> churches 
            {"z", "ces"},  //vez -> veces
            {"g", "gues"}, //zigzag -> zigzagues
            {"c", "ques"}, //frac -> fraques
            {"án", "anes"},
            {"én", "enes"},
            {"ín", "ines"},
            {"ón", "ones"},
            {"ún", "unes"},
        };

        char[] vowels = new[] { 'a', 'e', 'i', 'o', 'u', 'á', 'é', 'í', 'ó', 'ú' };

        public string MakePlural(string singularName)
        {
            if(string.IsNullOrEmpty(singularName))
                return singularName;

            int index = singularName.IndexOf(' '); 

            if(index != -1)
                return MakePlural(singularName.Substring(0, index)) + singularName.Substring(index);

            var result = exceptions.FirstOrDefault(r => singularName.EndsWith(r.Key));
            if (result.Value != null)
                return singularName.Substring(0, singularName.Length - result.Key.Length) + result.Value;

            char lassChar = singularName[singularName.Length - 1];
            if (vowels.Contains(lassChar))
                return singularName + "s";
            else
                return singularName + "es";
        }
    }
}
