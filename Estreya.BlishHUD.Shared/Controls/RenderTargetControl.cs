namespace Estreya.BlishHUD.Shared.Controls;

using Blish_HUD;
using Blish_HUD.Controls;
using Estreya.BlishHUD.Shared.Utils;
using Glide;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public abstract class RenderTargetControl : Control
{
    private RenderTarget2D _renderTarget;
    private bool _renderTargetIsEmpty;

    private bool _currentVisibilityDirection = false;
    private Tween _currentVisibilityAnimation { get; set; }

    private TimeSpan _lastDraw = TimeSpan.Zero;

    public TimeSpan DrawInterval { get; set; } = TimeSpan.FromMilliseconds(500);

    public new Point Size
    {
        get => base.Size;
        set
        {
            base.Size = value;
            this.CreateRenderTarget();
        }
    }

    public new bool Visible
    {
        get
        {
            if (this._currentVisibilityDirection && this._currentVisibilityAnimation != null)
            {
                return true;
            }

            if (!this._currentVisibilityDirection && this._currentVisibilityAnimation != null)
            {
                return false;
            }

            return base.Visible;
        }
        set => base.Visible = value;
    }

    protected override void Paint(SpriteBatch spriteBatch, Rectangle bounds)
    {
        spriteBatch.GraphicsDevice.PresentationParameters.RenderTargetUsage = RenderTargetUsage.PreserveContents;
        spriteBatch.End();

        if (this._renderTargetIsEmpty || this._lastDraw > this.DrawInterval)
        {
            spriteBatch.GraphicsDevice.SetRenderTarget(this._renderTarget);

            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            spriteBatch.GraphicsDevice.Clear(Color.Transparent); // Clear render target to transparent. Backgroundcolor is set on the control

            this.DoPaint(spriteBatch, bounds);
            
            spriteBatch.End();

            spriteBatch.GraphicsDevice.SetRenderTarget(null);

            this._renderTargetIsEmpty = false;
            this._lastDraw = TimeSpan.Zero;
        }

        spriteBatch.Begin(this.SpriteBatchParameters);
        spriteBatch.DrawOnCtrl(this, _renderTarget, bounds , Color.White);
        spriteBatch.End();

        spriteBatch.Begin(this.SpriteBatchParameters);
    }

    public sealed override void DoUpdate(GameTime gameTime)
    {
        this._lastDraw += gameTime.ElapsedGameTime;
        this.InternalUpdate(gameTime);
    }

    protected virtual void InternalUpdate(GameTime gameTime) { /* NOOP */ }

    protected abstract void DoPaint(SpriteBatch spriteBatch, Rectangle bounds);

    public new void Show()
    {
        if (this.Visible && this._currentVisibilityAnimation == null)
        {
            return;
        }

        if (this._currentVisibilityAnimation != null)
        {
            this._currentVisibilityAnimation.Cancel();
        }

        this._currentVisibilityDirection = true;
        this.Visible = true;
        this._currentVisibilityAnimation = Animation.Tweener.Tween(this, new { Opacity = 1f }, 0.2f);
        this._currentVisibilityAnimation.OnComplete(() =>
        {
            this._currentVisibilityAnimation = null;
        });
    }

    public new void Hide()
    {
        if (!this.Visible && this._currentVisibilityAnimation == null)
        {
            return;
        }

        if (this._currentVisibilityAnimation != null)
        {
            this._currentVisibilityAnimation.Cancel();
        }

        this._currentVisibilityDirection = false;
        this._currentVisibilityAnimation = Animation.Tweener.Tween(this, new { Opacity = 0f }, 0.2f);
        this._currentVisibilityAnimation.OnComplete(() =>
        {
            this.Visible = false;
            this._currentVisibilityAnimation = null;
        });
    }

    private void CreateRenderTarget()
    {
        int width = Math.Max(this.Width, 1);
        int height = Math.Max(this.Height, 1);

        if (this._renderTarget != null && (this._renderTarget.Width != width || this._renderTarget.Height != height))
        {
            this._renderTarget.Dispose();
            this._renderTarget = null;
        }

        if (this._renderTarget == null)
        {
            this._renderTarget = new RenderTarget2D(
            GameService.Graphics.GraphicsDevice,
            width,
            height,
            false,
            GameService.Graphics.GraphicsDevice.PresentationParameters.BackBufferFormat,
            DepthFormat.Depth24Stencil8, 1, RenderTargetUsage.PreserveContents);

            _renderTargetIsEmpty = true;
        }
    }

    protected override void DisposeControl()
    {
        if (this._renderTarget != null)
        {
            this._renderTarget?.Dispose();
            this._renderTarget = null;
        }

        if (this._currentVisibilityAnimation != null)
        {
            this._currentVisibilityAnimation.Cancel();
            this._currentVisibilityAnimation = null;
        }

        base.DisposeControl();
    }
}
