﻿namespace PluginGenerator
{
    /// <summary>
    /// Encapsulates the interactions with the JDK components
    /// </summary>
    public interface IJdkWrapper
    {
        bool IsJdkInstalled();

        bool CompileJar(string jarContentDirectory, string manifestFilePath, string fullJarPath, ILogger logger);
    }
}