#if __MICROVERSE_VEGETATION__
using JBooth.MicroVerseCore;
#endif
using UnityEngine;

namespace Rowlan.Yapp
{
    [System.Serializable]
    public class MicroVerseSettings
    {

#if __MICROVERSE_VEGETATION__
    public CopyPasteStamp copyPasteStamp = null;
#endif
    }
}