using HVR.Interface;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HVR.Editor
{
    class AssetPostprocessor : UnityEditor.AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            List<string> changedAssets = new List<string>();
            changedAssets.AddRange(importedAssets);
            changedAssets.AddRange(deletedAssets);
            changedAssets.AddRange(movedAssets);
            changedAssets.AddRange(movedFromAssetPaths);

            string projectAssetPath = Application.dataPath;
            projectAssetPath = projectAssetPath.Substring(0, projectAssetPath.Length - "Assets".Length);

            List<HvrActor> hvrActors = new List<HvrActor>();
            HvrScene.GetObjects(hvrActors);

            foreach (HvrActor actor in hvrActors)
            {
                if (actor.dataMode == HvrActor.eDataMode.reference &&
                    !string.IsNullOrEmpty(actor.data))
                {
                    // Get the path to the data that the hvrActor wants to load
                    string dataPath = string.Empty;
                    dataPath = HvrHelper.GetDataPathFromGUID(actor.data);

                    bool forceUpdate = false;

                    if (!string.IsNullOrEmpty(dataPath))
                    {
                        FileInfo fileInfo_hvrActorData = new FileInfo(dataPath);
                        FileAttributes fileAttr_hvrActorData = File.GetAttributes(dataPath);

                        for (int j = 0; j < changedAssets.Count; j++)
                        {
                            FileInfo changedAssetFileInfo = new FileInfo(projectAssetPath + changedAssets[j]);

                            if ((fileAttr_hvrActorData & FileAttributes.Directory) == FileAttributes.Directory)
                            {
                                if (fileInfo_hvrActorData.FullName == changedAssetFileInfo.Directory.FullName)
                                {
                                    forceUpdate = true;
                                }
                            }
                            else
                            {
                                if (fileInfo_hvrActorData.FullName == changedAssetFileInfo.FullName)
                                {
                                    forceUpdate = true;
                                }
                            }
                        }
                    }
                    else
                    {
                        // Assume that the data has been deleted from the project if the
                        // data path is empty, but the hvrActor has an hvrAsset assigned
                        if (actor.assetInterface != null)
                        {
                            forceUpdate = true;
                        }
                    }

                    if (forceUpdate)
                    {
                        if (string.IsNullOrEmpty(dataPath))
                        {
                            actor.CreateAsset(string.Empty, actor.dataMode);
                        }
                        else
                        {
                            actor.CreateAsset(actor.data, HvrActor.eDataMode.reference);
                        }
                    }
                }
            }
        }
    }
}