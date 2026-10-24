
#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;

using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;

public static class ProjectScriptExporter
{
    private const string MenuPath =
        "Tools/Project Script Exporter/Export All Scripts";

    [MenuItem(MenuPath)]
    public static void ExportAllScripts()
    {
        string projectRoot = Directory.GetParent(
            Application.dataPath
        ).FullName;

        string assetsPath = Application.dataPath;

        string defaultFileName =
            "Unity_Project_Scripts_" +
            DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") +
            ".txt";

        string savePath = EditorUtility.SaveFilePanel(
            "Export Unity Project Scripts",
            projectRoot,
            defaultFileName,
            "txt"
        );

        if (string.IsNullOrEmpty(savePath))
        {
            Debug.Log("Script export cancelled.");
            return;
        }

        try
        {
            string[] scriptFiles = Directory.GetFiles(
                assetsPath,
                "*.cs",
                SearchOption.AllDirectories
            );

            Array.Sort(
                scriptFiles,
                StringComparer.OrdinalIgnoreCase
            );

            StringBuilder output = new StringBuilder();

            output.AppendLine(
                "UNITY PROJECT SCRIPT EXPORT"
            );

            output.AppendLine(
                "========================================"
            );

            output.AppendLine(
                "Project: " + Application.productName
            );

            output.AppendLine(
                "Unity Version: " + Application.unityVersion
            );

            output.AppendLine(
                "Export Date: " + DateTime.Now
            );

            output.AppendLine(
                "Total Scripts: " + scriptFiles.Length
            );

            output.AppendLine(
                "========================================"
            );

            output.AppendLine();
            output.AppendLine();

            output.AppendLine(
                "SCRIPT INDEX"
            );

            output.AppendLine(
                "========================================"
            );

            for (int i = 0; i < scriptFiles.Length; i++)
            {
                string relativePath =
                    GetRelativeProjectPath(
                        projectRoot,
                        scriptFiles[i]
                    );

                output.AppendLine(
                    (i + 1).ToString("D3") +
                    " | " + relativePath
                );
            }

            output.AppendLine();
            output.AppendLine();

            output.AppendLine(
                "FULL SCRIPT SOURCE CODE"
            );

            output.AppendLine(
                "========================================"
            );

            output.AppendLine();

            int exportedCount = 0;

            List<string> failedScripts =
                new List<string>();

            for (int i = 0; i < scriptFiles.Length; i++)
            {
                string filePath = scriptFiles[i];

                string relativePath =
                    GetRelativeProjectPath(
                        projectRoot,
                        filePath
                    );

                EditorUtility.DisplayProgressBar(
                    "Exporting Unity Scripts",
                    relativePath,
                    (float)(i + 1) /
                    Mathf.Max(1, scriptFiles.Length)
                );

                try
                {
                    string sourceCode =
                        File.ReadAllText(filePath);

                    output.AppendLine(
                        "##################################################"
                    );

                    output.AppendLine(
                        "SCRIPT " + (i + 1)
                    );

                    output.AppendLine(
                        "FILE: " + relativePath
                    );

                    output.AppendLine(
                        "##################################################"
                    );

                    output.AppendLine();

                    output.AppendLine(
                        "----- BEGIN SOURCE CODE -----"
                    );

                    output.Append(sourceCode);

                    if (!sourceCode.EndsWith("\n"))
                        output.AppendLine();

                    output.AppendLine(
                        "----- END SOURCE CODE -----"
                    );

                    output.AppendLine();
                    output.AppendLine();

                    exportedCount++;
                }
                catch (Exception exception)
                {
                    failedScripts.Add(
                        relativePath +
                        " | " + exception.Message
                    );

                    Debug.LogWarning(
                        "Could not export script: " +
                        relativePath +
                        "\n" +
                        exception.Message
                    );
                }
            }

            if (failedScripts.Count > 0)
            {
                output.AppendLine(
                    "FAILED SCRIPT EXPORTS"
                );

                output.AppendLine(
                    "========================================"
                );

                foreach (string failed in failedScripts)
                {
                    output.AppendLine(failed);
                }
            }

            File.WriteAllText(
                savePath,
                output.ToString(),
                new UTF8Encoding(false)
            );

            Debug.Log(
                "PROJECT SCRIPT EXPORT COMPLETE\n" +
                "Exported: " + exportedCount + "\n" +
                "Failed: " + failedScripts.Count + "\n" +
                "Saved To: " + savePath
            );

            EditorUtility.DisplayDialog(
                "Export Complete",
                "Successfully exported " +
                exportedCount +
                " scripts.\n\n" +
                "Failed: " +
                failedScripts.Count +
                "\n\nSaved to:\n" +
                savePath,
                "OK"
            );

            EditorUtility.RevealInFinder(savePath);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "Project script export failed:\n" +
                exception
            );

            EditorUtility.DisplayDialog(
                "Export Failed",
                exception.Message,
                "OK"
            );
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static string GetRelativeProjectPath(
        string projectRoot,
        string absolutePath
    )
    {
        string normalizedRoot =
            Path.GetFullPath(projectRoot)
                .Replace('\\', '/')
                .TrimEnd('/') + "/";

        string normalizedFile =
            Path.GetFullPath(absolutePath)
                .Replace('\\', '/');

        if (normalizedFile.StartsWith(
            normalizedRoot,
            StringComparison.OrdinalIgnoreCase
        ))
        {
            return normalizedFile.Substring(
                normalizedRoot.Length
            );
        }

        return normalizedFile;
    }
}

#endif
