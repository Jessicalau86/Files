﻿using ByteSizeLib;
using Files.Enums;
using Files.Extensions;
using Files.Filesystem;
using Microsoft.Toolkit.Uwp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Core;
using static Files.Helpers.NativeFindStorageItemHelper;
using FileAttributes = System.IO.FileAttributes;

namespace Files.ViewModels.Properties
{
    public abstract class BaseProperties
    {
        public IShellPage AppInstance { get; set; } = null;
        public CoreDispatcher Dispatcher { get; set; }
        public CancellationTokenSource TokenSource { get; set; }
        public SelectedItemsPropertiesViewModel ViewModel { get; set; }

        public async Task<long> CalculateFolderSizeAsync(string path, CancellationToken token)
        {
            if (string.IsNullOrEmpty(path))
            {
                // In MTP devices calculating folder size would be too slow
                // Also should use StorageFolder methods instead of FindFirstFileExFromApp
                return 0;
            }

            long size = 0;
            FINDEX_INFO_LEVELS findInfoLevel = FINDEX_INFO_LEVELS.FindExInfoBasic;
            int additionalFlags = FIND_FIRST_EX_LARGE_FETCH;

            IntPtr hFile = FindFirstFileExFromApp(path + "\\*.*", findInfoLevel, out WIN32_FIND_DATA findData, FINDEX_SEARCH_OPS.FindExSearchNameMatch, IntPtr.Zero,
                                                  additionalFlags);

            var count = 0;
            if (hFile.ToInt64() != -1)
            {
                do
                {
                    if (((FileAttributes)findData.dwFileAttributes & FileAttributes.Directory) != FileAttributes.Directory)
                    {
                        size += findData.GetSize();
                        ++count;
                        ViewModel.FilesCount++;
                    }
                    else if (((FileAttributes)findData.dwFileAttributes & FileAttributes.Directory) == FileAttributes.Directory)
                    {
                        if (findData.cFileName != "." && findData.cFileName != "..")
                        {
                            var itemPath = Path.Combine(path, findData.cFileName);

                            size += await CalculateFolderSizeAsync(itemPath, token);
                            ++count;
                            ViewModel.FoldersCount++;
                        }
                    }

                    if (size > ViewModel.ItemSizeBytes)
                    {
                        await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                        {
                            ViewModel.ItemSizeBytes = size;
                            ViewModel.ItemSize = ByteSize.FromBytes(size).ToBinaryString().ConvertSizeAbbreviation();
                            SetItemsCountString();
                        });
                    }

                    if (token.IsCancellationRequested)
                    {
                        break;
                    }
                } while (FindNextFile(hFile, out findData));
                FindClose(hFile);
                return size;
            }
            else
            {
                return 0;
            }
        }

        public abstract void GetBaseProperties();

        public async void GetOtherProperties(StorageItemContentProperties properties)
        {
            string dateAccessedProperty = "System.DateAccessed";
            string fileOwnerProperty = "System.FileOwner";
            List<string> propertiesName = new List<string>();
            propertiesName.Add(dateAccessedProperty);
            propertiesName.Add(fileOwnerProperty);
            IDictionary<string, object> extraProperties = await properties.RetrievePropertiesAsync(propertiesName);

            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            string returnformat = Enum.Parse<TimeStyle>(localSettings.Values[Constants.LocalSettings.DateTimeFormat].ToString()) == TimeStyle.Application ? "D" : "g";

            // Cannot get date and owner in MTP devices
            ViewModel.ItemAccessedTimestamp = ListedItem.GetFriendlyDateFromFormat((DateTimeOffset)(extraProperties[dateAccessedProperty] ?? DateTimeOffset.Now), returnformat);

            if (App.AppSettings.ShowFileOwner)
            {
                // Cannot get date and owner in MTP devices
                ViewModel.ItemFileOwner = extraProperties[fileOwnerProperty]?.ToString();
            }
        }

        public abstract void GetSpecialProperties();

        public void SetItemsCountString()
        {
            if (ViewModel.LocationsCount > 0)
            {
                ViewModel.FilesAndFoldersCountString = string.Format("PropertiesFilesFoldersAndLocationsCountString".GetLocalized(), ViewModel.FilesCount, ViewModel.FoldersCount, ViewModel.LocationsCount);
            }
            else
            {
                ViewModel.FilesAndFoldersCountString = string.Format("PropertiesFilesAndFoldersCountString".GetLocalized(), ViewModel.FilesCount, ViewModel.FoldersCount);
            }
        }
    }
}