using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;
using UnityEngine;

public static class CachingModule
{

    /*
        Caching module:
        SaveDataToCache - saves given string file to cache, returns true if successful
        LoadDataFromCache - loads given string file from cache, returns empty if file doesnt exist
        DeleteDataFromCache - Deletes given string data from cache, returns true of successful
        CheckIfCachedDataExists - returns true if data exists in cache
    */

    const string CacheFolderName = "SPOILZ";

    public static bool SaveDataToCache(string DataToSave, string FileName)
    {
        string folderPath = Path.Combine(Application.persistentDataPath, CacheFolderName);

        Debug.Log($"SaveDataToCache Path: \n{folderPath}");

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string path = Path.Combine(folderPath, FileName);
        try
        {
            File.WriteAllText(path, DataToSave);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error saving data to cache: {e.Message}");
            return false;
        }

        return true;
    }

    public static string LoadDataFromCache(string FileName)
    {
        string folderPath = Path.Combine(Application.persistentDataPath, CacheFolderName);
        string path = Path.Combine(folderPath, FileName);

        Debug.Log($"SaveDataToCache Path: \n{folderPath}");
        
        if (File.Exists(path))
        {
            return File.ReadAllText(path);
        }

        return string.Empty;  // Return empty if the file doesn't exist
    }

    public static bool SaveDataToCache_ByteArray(byte[] _dataToSave, string _fileName)
    {
        string folderPath = Path.Combine(Application.persistentDataPath, CacheFolderName);

        Debug.Log($"SaveDataToCache Path: \n{folderPath}");

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string path = Path.Combine(folderPath, _fileName);
        try
        {
            File.WriteAllBytes(path, _dataToSave);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error saving byte array to cache: {e.Message}");
            return false;
        }

        return true;
    }

    public static byte[] LoadDataFromCache_ByteArray(string _fileName)
    {
        string folderPath = Path.Combine(Application.persistentDataPath, CacheFolderName);
        string path = Path.Combine(folderPath, _fileName);

        Debug.Log($"SaveDataToCache Path: \n{folderPath}");

        if (File.Exists(path))
        {
            return File.ReadAllBytes(path);
        }

        return new byte[0];  // Return an empty byte array if the file doesn't exist
    }

    public static bool DeleteDataFromCache(string FileName)
    {
        string folderPath = Path.Combine(Application.persistentDataPath, CacheFolderName);
        string path = Path.Combine(folderPath, FileName);

        Debug.Log($"SaveDataToCache Path: \n{folderPath}");

        if (File.Exists(path))
        {
            File.Delete(path);
            return true;
        }

        return false;
    }

    public static bool CheckIfCachedDataExists(string FileName)
    {
        string folderPath = Path.Combine(Application.persistentDataPath, CacheFolderName);
        string path = Path.Combine(folderPath, FileName);

        Debug.Log($"SaveDataToCache Path: \n{folderPath}");

        return File.Exists(path);
    }
}