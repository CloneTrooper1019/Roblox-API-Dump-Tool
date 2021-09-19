﻿using System.Collections.Generic;
using System.Linq;

namespace RobloxApiDumpTool
{
    public class DiffChangeList : List<object>
    {
        public string Name { get; private set; }

        public DiffChangeList(string name = "ChangeList")
        {
            Name = name;
        }

        private void PreformatList()
        {
            object lastChange = null;

            foreach (object change in this)
            {
                if (lastChange is Parameters)
                    if (change is LuaType type)
                        type.IsReturnType = true;

                lastChange = change;
            }
        }

        public string ListElements(string separator, string prefix = "")
        {
            PreformatList();

            string[] elements = this
                .Select(elem => prefix + elem.ToString())
                .ToArray();

            return string.Join(separator, elements);
        }

        public override string ToString()
        {
            return ListElements(" ");
        }

        public void WriteHtml(ReflectionDumper buffer, bool multiline = false, int extraTabs = 0, Descriptor.HtmlConfig config = null)
        {
            if (config == null)
                config = new Descriptor.HtmlConfig();

            int numTabs;

            if (multiline)
            {
                buffer.OpenClassTag(Name, extraTabs + 1, "div");
                buffer.NextLine();

                buffer.OpenClassTag("ChangeList", extraTabs + 2);
                numTabs = 3;
            }
            else
            {
                buffer.OpenClassTag(Name, extraTabs + 1);
                numTabs = 2;
            }

            numTabs += extraTabs;

            if (config.NumTabs == 0)
                config.NumTabs = numTabs;

            buffer.NextLine();
            PreformatList();

            foreach (object change in this)
            {
                if (change is Parameters)
                {
                    var parameters = change as Parameters;
                    parameters.WriteHtml(buffer, numTabs, true);
                }
                else if (change is LuaType)
                {
                    var type = change as LuaType;
                    type.WriteHtml(buffer, numTabs);
                }
                else if (change is Descriptor)
                {
                    var desc = change as Descriptor;
                    desc.WriteHtml(buffer, config);
                }
                else
                {
                    string value;

                    if (change is Security)
                    {
                        var security = change as Security;
                        value = security.Describe(true);
                    }
                    else
                    {
                        value = change.ToString();
                    }

                    string tagClass;

                    if (value.Contains("🧬"))
                        tagClass = "ThreadSafety";
                    else if (value.StartsWith("["))
                        tagClass = "Serialization";
                    else if (value.StartsWith("{"))
                        tagClass = "Security";
                    else if (value.StartsWith("\""))
                        tagClass = "String";
                    else
                        tagClass = change.GetType().Name;

                    if (tagClass == "Security" && value.Contains("None"))
                        tagClass += " darken";

                    buffer.WriteElement(tagClass, value, numTabs);
                }
            }

            buffer.CloseClassTag(numTabs - 1);

            if (multiline)
            {
                buffer.CloseClassTag(1, "div");
            }
        }
    }
}