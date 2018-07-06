using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HVR
{
    public class HvrActorShaderSubroutineStack
    {
        private string[] m_shaderArray = new string[0];
        private int[] m_hashCheck = new int[0];
        private bool[] m_enabledCheck = new bool[0];

#if UNITY_EDITOR
        private long[] m_assetWriteTime = new long[0];
#endif

        public bool Update(HvrActorShaderSubroutineBase[] subroutines)
        {
            bool changed = false;

            if (m_hashCheck.Length != subroutines.Length)
            {
                m_hashCheck = new int[subroutines.Length];
                m_enabledCheck = new bool[subroutines.Length];

#if UNITY_EDITOR
                m_assetWriteTime = new long[subroutines.Length];
#endif
                changed = true;
            }

            for (int i = 0; i < subroutines.Length; i++)
            {
                HvrActorShaderSubroutineBase subroutine = subroutines[i];

#if UNITY_EDITOR
                if (subroutine.textAsset != null)
                {
                    long writeTime = EditorHelper.GetAssetLastWriteTimeTicks(subroutine.textAsset);

                    if (writeTime > m_assetWriteTime[i])
                    {
                        m_assetWriteTime[i] = writeTime;
                        AssetDatabase.Refresh();
                        changed = true;
                    }
                }
#endif

                if (m_enabledCheck[i] != subroutine.isActiveAndEnabled)
                {
                    m_enabledCheck[i] = subroutines[i].isActiveAndEnabled;
                    changed = true;
                }

                if (subroutine.isActiveAndEnabled)
                {
                    if (subroutine.textAsset != null)
                    {
                        int hashCode = subroutines[i].textAsset.GetHashCode();

                        // If the text asset has changed, then we need to update the shaderarray
                        if (m_hashCheck[i] != hashCode)
                        {
                            m_hashCheck[i] = hashCode;
                            changed = true;
                        }
                    }
                }
            }

            if (changed)
            {
                List<string> shaderList = new List<string>();

                for (int i = 0; i < subroutines.Length; i++)
                {
                    if (subroutines[i] != null &&
                        subroutines[i].isActiveAndEnabled &&
                        subroutines[i].textAsset != null)
                    {
                        string code = subroutines[i].textAsset.text;

                        if (!string.IsNullOrEmpty(code))
                        {
                            code = code.Replace(HvrActorShaderSubroutineBase.UNIQUE_ID_IDENTIFIER, subroutines[i].id);
                            shaderList.Add(code);
                        }
                    }
                }

                m_shaderArray = shaderList.ToArray();
            }

            return changed;
        }

        public string[] GetShaderArray()
        {
            return m_shaderArray;
        }
    }
}
