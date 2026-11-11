using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ClockWidget;

/// <summary>Satu karakter jam yang berganti isi dengan animasi.</summary>
public sealed class AnimatedText : Grid
{
    readonly TextBlock _old = new(), _new = new();
    readonly TranslateTransform _oldMove = new(), _newMove = new();
    readonly ScaleTransform _flip = new();

    public AnimatedText()
    {
        ClipToBounds = true;
        RenderTransformOrigin = new Point(0.5, 0.5);
        RenderTransform = _flip;
        _old.RenderTransform = _oldMove;
        _new.RenderTransform = _newMove;
        _old.Opacity = 0;
        foreach (var tb in new[] { _old, _new })
        {
            tb.HorizontalAlignment = HorizontalAlignment.Center;
            Typography.SetNumeralAlignment(tb, FontNumeralAlignment.Tabular);
            Children.Add(tb);
        }
    }

    public void Set(string text, AnimationKind kind, bool animate)
    {
        if (text == _new.Text) return;
        Reset();
        _old.Text = _new.Text;
        _new.Text = text;
        if (!animate || !IsLoaded || ActualHeight <= 0) return;

        switch (kind)
        {
            case AnimationKind.Fade:
            {
                var d = TimeSpan.FromMilliseconds(380);
                _old.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, d));
                _new.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, d));
                break;
            }
            case AnimationKind.Slide:
            {
                var d = TimeSpan.FromMilliseconds(340);
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                double h = ActualHeight;
                _oldMove.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -h, d) { EasingFunction = ease });
                _newMove.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(h, 0, d) { EasingFunction = ease });
                _old.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, d));
                break;
            }
            case AnimationKind.Flip:
            {
                var half = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(160));
                var end = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(320));
                var scale = new DoubleAnimationUsingKeyFrames();
                scale.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                scale.KeyFrames.Add(new EasingDoubleKeyFrame(0, half, new QuadraticEase { EasingMode = EasingMode.EaseIn }));
                scale.KeyFrames.Add(new EasingDoubleKeyFrame(1, end, new QuadraticEase { EasingMode = EasingMode.EaseOut }));
                _flip.BeginAnimation(ScaleTransform.ScaleYProperty, scale);

                var oldOpacity = new DoubleAnimationUsingKeyFrames();
                oldOpacity.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                oldOpacity.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, half));
                _old.BeginAnimation(OpacityProperty, oldOpacity);

                var newOpacity = new DoubleAnimationUsingKeyFrames();
                newOpacity.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                newOpacity.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, half));
                _new.BeginAnimation(OpacityProperty, newOpacity);
                break;
            }
        }
    }

    void Reset()
    {
        _old.BeginAnimation(OpacityProperty, null);
        _new.BeginAnimation(OpacityProperty, null);
        _oldMove.BeginAnimation(TranslateTransform.YProperty, null);
        _newMove.BeginAnimation(TranslateTransform.YProperty, null);
        _flip.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        _old.Opacity = 0;
        _new.Opacity = 1;
        _oldMove.Y = 0;
        _newMove.Y = 0;
        _flip.ScaleY = 1;
    }
}
