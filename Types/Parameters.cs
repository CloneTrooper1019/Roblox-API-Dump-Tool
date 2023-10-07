﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace RobloxApiDumpTool
{
    public class Parameter
    {
        private const string quote = "\"";

        public LuaType Type;
        public string Name;
        public string Default;

        public override string ToString()
        {
            if (Default != null && !Type.Name.EndsWith("?"))
                Type.Name += "?";

            string result = $"{Name}: {Type}";
            string category = $"{Type.Category}";

            if (Default != null)
            {
                if (Type.Name == "string" || category == "Enum")
                    if (!Default.StartsWith(quote) && !Default.EndsWith(quote))
                        Default = quote + Default + quote;

                if (Type.Category == TypeCategory.DataType && Type.Name != "Function")
                    Default = $"{Type.Name}.new()";

                if (Default.Length == 0)
                    return result;

                result += " = " + Default;
            }
            
            return result;
        }

        public void WriteHtml(ReflectionHtml html)
        {
            string name = Name;
            LuaType luaType = Type;
            string paramDef = Default;

            if (paramDef != null && !luaType.Name.EndsWith("?"))
                luaType.Name += "?";

            html.OpenSpan("Parameter", () =>
            {
                html.Span("ParamName", name);
                html.Symbol(": ");

                luaType.WriteHtml(html);

                // Write Default
                if (paramDef != null && paramDef != "nil")
                {
                    string typeLbl = luaType.GetSignature();
                    string typeName = luaType.Name;
                    
                    if (luaType.Category == TypeCategory.DataType && typeName != "Function")
                    {
                        html.Span("Type", typeName);
                        html.Symbol(".");
                        html.Span("Name", "new");
                        html.Symbol("()");
                    }
                    else
                    {
                        if (luaType.Category == TypeCategory.Enum)
                            typeName = "String";
                        else
                            typeName = luaType.LuauType;

                        html.Symbol(" = ");
                        html.Span(typeName, paramDef);
                    }
                }
            });
        }
    }

    public class Parameters : List<Parameter>
    {
        public override string ToString()
        {
            string[] parameters = this.Select(param => param.ToString()).ToArray();
            return '(' + string.Join(", ", parameters) + ')';
        }

        public void WriteHtml(ReflectionHtml html, bool diffMode = false)
        {
            string paramsTag = "Parameters";
            IEnumerable<Parameter> parameters = this;

            if (diffMode)
                paramsTag += " change";

            html.OpenSpan(paramsTag, () =>
            {
                html.Symbol("(");

                for (int i = 0; i < Count; i++)
                {
                    var param = this[i];

                    if (i > 0)
                        html.Symbol(", ");

                    param.WriteHtml(html);
                }

                html.Symbol(")");
            });
        }
    }
}