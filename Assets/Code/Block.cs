using UnityEngine;

namespace FractalCompression
{
    public class PIFS
    {
        public Vector2Int Pos;
        public int Flip;
        public int Rotate ;
        public float Contrast;
        public float Brightness;
    };
    
    public class Block
    {
        public Vector2Int Pos { get; set; }
        public int Size => Data.GetLength(0);
        public float[,] Data { get; set; }

        public Block Link { get; set; }

        public PIFS Pifs { get; set; }  = new PIFS();
        
        public Block()
        {
        }

        public Block (Vector2Int pos, float[,] source)
        {
            Pos = pos;
            Data = source;
        }

        public Block Copy()
        {
            var copyData = new float[Size, Size];
            for (var i = 0; i < Size; i++)
            {
                for (var j = 0; j < Size; j++)
                {
                    copyData[i, j] = Data[i, j];
                }
            }

            var result = new Block(Pos, copyData);
            return result;
        }

        public float this [int index] 
        {
            get => Data[index%Size, index/Size];
            set => Data[index%Size, index/Size] = value;
        }

        public static Block operator * (Block block, float c)
        {
            var result = block.Copy();
            for (var i = 0; i < result.Size*result.Size; i++)
            {
                result[i] *= c;
            }

            return result;
        }
        
        public static Block operator + (Block block, float c)
        {
            var result = block.Copy();
            for (var i = 0; i < result.Size*result.Size; i++)
            {
                result[i] += c;
            }

            return result;
        }

        public float GetSimilarity (Block domain)
        {
            var result = 0f;
            var n = Size*Size;
            for (var i = 0; i < n; i++)
            {
                result += (this[i]-domain[i])*(this[i]-domain[i]);
            }
            return result;
        }

        public (float, float) FindContrastAndBrightness_Simple(Block domain)
        {
            var contrast = 0.75f;
            var sum = 0f;
            var size = Size*Size;
            for (var i = 0; i < size; i++)
            {
                sum += (this[i]-contrast*domain[i]);
            }
            var brightness = sum/size;
                
            return (contrast, brightness);
        }
        
        
        //Метод наименьших квадратов
        //http://www.cleverstudents.ru/articles/mnk.html
        public (float, float) FindContrastAndBrightness(Block domain)
        {
            var n = Size*Size;
            var sumXY = 0f;
            var sumX = 0f;
            var sumY = 0f;
            var sumXX = 0f;
            for (var i = 0; i < n; i++)
            {
                sumXY += this[i]*domain[i];
                sumX += this[i];
                sumY += domain[i];
                sumXX += this[i]*this[i];
            }
            var contrast = (n*sumXY-sumX*sumY)/(n*sumXX - sumX*sumX);
            var brightness = (sumY - contrast*sumX)/n;

            if (contrast == float.NaN || brightness == float.NaN)
            {
                contrast = 1;
                brightness = 0;
            }

            return (contrast, brightness);
        }

        public void Decompress(float[,] data)
        {
            Data = data;
            Flip(Pifs.Flip);
            Rotate(Pifs.Rotate);
            for (int i = 0; i < Size*Size; i++)
            {
                this[i] = Pifs.Contrast*this[i]+Pifs.Brightness;
            }
            Reduce();
        }
        
        public void Rotate(int angle)
        {
            Pifs.Rotate = Mathf.Max(Pifs.Rotate, angle);
            if (angle <= 0)
                return;
            
            var result = new float[Size, Size];
            for (var i = 0; i < Size; i++)
            {
                for (var j = 0; j < Size; j++)
                {
                    result[Size-j-1, i] = Data[i, j];
                }
            }

            Data = result;
            Rotate(angle-1);
        }

        public void Flip (int flip)
        {
            if(flip<=0 || flip>2)
                return;
            
            Pifs.Flip = flip;
            var result = new float[Size, Size];
            for (var i = 0; i < Size; i++)
            {
                for (var j = 0; j < Size; j++)
                {
                    if (flip == 1)
                    {
                        result[Size-i-1, j] = Data[i, j];
                    }
                    else if (flip == 2)
                    {
                        result[i, Size-j-1] = Data[i, j];
                    }
                }
            }
        }
        
        public void Reduce()
        {
            const int factor = 2;
            var result = new float[Size/factor, Size/factor];
            for (var i = 0; i < Size/factor; i++)
            {
                for (var j = 0; j < Size/factor; j++)
                {
                    result[i, j] = 0.25f*(Data[i*factor, j*factor]+Data[i*factor+1, j*factor]+Data[i*factor, j*factor+1]+
                                          Data[i*factor+1, j*factor+1]);
                }
            }

            Data = result;
        }
        
        public void Increase()
        {
            const int factor = 2;
            var result = new float[Size*factor, Size*factor];
            for (var i = 0; i < Size*factor; i++)
            {
                for (var j = 0; j < Size*factor; j++)
                {
                    result[i, j] = Data[i/factor, j/factor];
                }
            }

            Data = result;
        }
    };
}