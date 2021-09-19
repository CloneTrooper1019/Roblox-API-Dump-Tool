﻿using System.Collections.Generic;
using System.Linq;

namespace RobloxApiDumpTool
{
    public sealed class MergeRenamedClasses : IDiffModifier
    {
        public ModifierOrder Order => ModifierOrder.PreMemberDiff;

        public void RunModifier(ref List<Diff> diffs)
        {
            List<Diff> memberedClassDiffs = diffs
                .Where(diff =>  diff.Target is ClassDescriptor)
                .Where(diff => (diff.Target as ClassDescriptor).Members.Count > 0)
                .ToList();

            List<Diff> newClassDiffs = memberedClassDiffs
                .Where(diff => diff.Type == DiffType.Add)
                .ToList();

            List<Diff> oldClassDiffs = memberedClassDiffs
                .Where(diff => diff.Type == DiffType.Remove)
                .ToList();

            if (oldClassDiffs.Count > 0 && newClassDiffs.Count > 0)
            {
                foreach (Diff newClassDiff in newClassDiffs)
                {
                    // Ignore disposed diffs.
                    if (newClassDiff.Disposed)
                        continue;

                    // Grab the summary version of the new diff.
                    var newClass = newClassDiff.Target as ClassDescriptor;
                    string newDiff = newClassDiff.WriteDiffTxt(false);

                    foreach (Diff oldClassDiff in oldClassDiffs)
                    {
                        // Ignore disposed diffs.
                        if (oldClassDiff.Disposed)
                            continue;

                        // Grab the summary version of the old diff.
                        var oldClass = oldClassDiff.Target as ClassDescriptor;
                        string oldDiff = oldClassDiff.WriteDiffTxt(false);

                        // Try to convert the old diff into the new diff generated above.
                        string nameChange = oldDiff
                            .Replace(oldClass.Name, newClass.Name)
                            .Replace("Removed", "Added");

                        // Intersect some of the old and new signatures to see if they are similar enough.
                        List<string> oldLines = newDiff.Split('\r', '\n')
                            .Select(oldLine => oldLine.Trim())
                            .Where(oldLine => oldLine.Length > 0)
                            .ToList();

                        List<string> newLines = nameChange.Split('\r', '\n')
                            .Select(newLine => newLine.Trim())
                            .Where(newLine => newLine.Length > 0)
                            .ToList();

                        List<string> intersects = oldLines
                            .Intersect(newLines)
                            .ToList();

                        // If the signatures match, then this is probably a renamed class?
                        if (intersects.Count >= ((oldLines.Count + newLines.Count) / 2))
                        {
                            // HACK: To allow the members to be compared nicely, I need to change the name 
                            // of the old class to the name of the new class. However, I still want to 
                            // describe the target of the ClassName change with the old ClassName, so I
                            // also have to create a dummy ClassDescriptor to serve as the target.

                            ClassDescriptor dummy = new ClassDescriptor();
                            dummy.Name = oldClass.Name;

                            // Create a diff describing the ClassName change.
                            Diff nameChangeDiff = new Diff()
                            {
                                Type = DiffType.Rename,

                                Field = "Class",
                                Target = dummy,
                                
                                To = { newClass.Name }
                            };

                            // Add this change to the diffs.
                            diffs.Add(nameChangeDiff);

                            // Remap the old class with the new class name.
                            var oldClasses = oldClass.Database.Classes;
                            var newName = newClass.Name;

                            oldClasses.Remove(oldClass.Name);
                            oldClass.Name = newName;
                            oldClasses.Add(newName, oldClass);

                            // Dispose the original class diffs.
                            oldClassDiff.Disposed = true;
                            newClassDiff.Disposed = true;
                        }
                    }
                }
            }
        }
    }
}