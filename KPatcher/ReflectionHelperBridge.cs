using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace KPatcher
{
    //Utility class for shorter calls
    public static class R
    {
        public static ReflectionHelper.Types T => Program.ReflectionHelper.T;
        public static ReflectionHelper.Properties P => Program.ReflectionHelper.P;
        public static ReflectionHelper.Fields F => Program.ReflectionHelper.F;
        public static ReflectionHelper.Methods M => Program.ReflectionHelper.M;

        //Make sure you didn't get the object type .ctor()
        public static ConstructorInfo C(Type type, Type[] parameters = null, bool searchForStatic = false) =>
            AccessTools.Constructor(type, parameters, searchForStatic);

        public static ConstructorInfo C(Type type, Func<ConstructorInfo, bool> predicate) =>
            AccessTools.FirstConstructor(type, predicate);

        public static List<ConstructorInfo> Cs(Type type, bool? searchForStatic = null) =>
            AccessTools.GetDeclaredConstructors(type, searchForStatic);
    }
}