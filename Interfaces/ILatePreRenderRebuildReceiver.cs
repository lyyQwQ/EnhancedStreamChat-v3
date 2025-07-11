using System;

namespace EnhancedStream_139.Interfaces
{
    public interface ILatePreRenderRebuildReceiver
    {
        void LatePreRenderRebuildHandler(object sender, EventArgs e);
    }
}