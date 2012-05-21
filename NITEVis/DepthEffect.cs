
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Media3D;

namespace NITEVis
{
    public class DepthEffect : ShaderEffect
    {
        public static readonly DependencyProperty InputProperty = ShaderEffect.RegisterPixelShaderSamplerProperty("Input", typeof(DepthEffect), 0);
        public static readonly DependencyProperty TexLabelProperty = ShaderEffect.RegisterPixelShaderSamplerProperty("TexLabel", typeof(DepthEffect), 1);
        public static readonly DependencyProperty TexDepthColorProperty = ShaderEffect.RegisterPixelShaderSamplerProperty("TexDepthColor", typeof(DepthEffect), 2);
        public static readonly DependencyProperty TexLabelColorProperty = ShaderEffect.RegisterPixelShaderSamplerProperty("TexLabelColor", typeof(DepthEffect), 3);
        
        public DepthEffect()
        {
            PixelShader = new PixelShader() { UriSource = new Uri("/NITEVis;component/DepthEffect.ps", UriKind.Relative) };

            this.UpdateShaderValue(InputProperty);
            this.UpdateShaderValue(TexLabelProperty);
            this.UpdateShaderValue(TexDepthColorProperty);
            this.UpdateShaderValue(TexLabelColorProperty);
        }

        public Brush Input
        {
            get
            {
                return ((Brush)(this.GetValue(InputProperty)));
            }
            set
            {
                this.SetValue(InputProperty, value);
            }
        }

        public Brush TexLabel
        {
            get
            {
                return ((Brush)(this.GetValue(TexLabelProperty)));
            }
            set
            {
                this.SetValue(TexLabelProperty, value);
            }
        }

        public Brush TexDepthColor
        {
            get
            {
                return ((Brush)(this.GetValue(TexDepthColorProperty)));
            }
            set
            {
                this.SetValue(TexDepthColorProperty, value);
            }
        }

        public Brush TexLabelColor
        {
            get
            {
                return ((Brush)(this.GetValue(TexLabelColorProperty)));
            }
            set
            {
                this.SetValue(TexLabelColorProperty, value);
            }
        }
    }
}
