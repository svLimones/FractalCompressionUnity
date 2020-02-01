using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FractalCompression
{
    public static class StateData
    {
        public static int RangeBlockSize = 4;
        public static int DomainBlockSize = 8;
        public static float[,] Data = new float[0,0];
    };
    
    
    public class FractalCompressor : MonoBehaviour
    {
        [SerializeField] private Texture2D ImageToCopress;
        [SerializeField] private Texture2D DecompressedImage;
        [SerializeField] private bool UseSimpleCompress = false;
        [SerializeField] private int DecompressIterationCount = 8;
        [SerializeField] private int ChanelToCopress = 0;
        private List<Block> rangeBlocks = new List<Block>();
        private List<Block> domainBlocks = new List<Block>();
        public List<PIFS> CompressedData { get; set; } = new List<PIFS>();
        private float[,] Data => StateData.Data; 
        private int Size => Data.GetLength(0);


        [ContextMenu("Compress")]
        public void Compress()
        {
            Compress(ImageToCopress.GetPixels(0));
        }
        
        public async void Compress (Color[] image)
        {
            var size = (int)Mathf.Sqrt(image.Length);
            CompressedData.Clear();
            var source = new float[size, size];
            for (var i = 0; i < size; i++)
            {
                for (var j = 0; j < size; j++)
                {
                    source[i, j] = image[i*size + j][ChanelToCopress];
                }
            }

            StateData.Data = source;

            Debug.Log(">>GenerateRangeBlocks");
            GenerateRangeBlocks();
            
            Debug.Log(">>GenerateDomainBlocks");
            await Task.Delay(TimeSpan.FromSeconds(1));

            
            GenerateDomainBlocks();
            
            Debug.Log("<<GenerateDomainBlocks");
            await Task.Delay(TimeSpan.FromSeconds(1));
            
            Debug.Log(">>Finding");
            await Task.Delay(TimeSpan.FromSeconds(1));
            
            FindBetter();
            
            Debug.Log("<<Finding");
            await Task.Delay(TimeSpan.FromSeconds(1));
            
            foreach (var block in rangeBlocks)
            {
                block.Pifs.Pos = block.Link.Pos;
                CompressedData.Add(block.Pifs);
            }
            Debug.Log("Compress success");
        }

        [ContextMenu("Decompress")]
        public void Decompress()
        {
            var size = (int)(Mathf.Sqrt(CompressedData.Count)) * StateData.RangeBlockSize;
            GenerateRandomSource(size);
            GenerateRangeBlocks();
            for(var i=0; i<rangeBlocks.Count; i++)
            {
                rangeBlocks[i].Pifs = CompressedData[i];
                Debug.Log($"flip={CompressedData[i].Flip}, rot={CompressedData[i].Rotate},b={CompressedData[i].Brightness}, c={CompressedData[i].Contrast}");
            }
            
            for (var iteration = 0; iteration < DecompressIterationCount; iteration++)
            {
                foreach (var block in rangeBlocks)
                {
                    var pos = block.Pifs.Pos;
                    var data = CutData(pos, StateData.DomainBlockSize );
                    block.Decompress(data);
                    for (var i = 0; i < block.Size; i++)
                    {
                        for (var j = 0; j < block.Size; j++)
                        {
                            Data[block.Pos.x+i, block.Pos.y+j] = block.Data[i, j];
                        }
                    }
                }
            }

            DecompressedImage = ExportToTexture();
            Debug.Log("Decompress success");
        }
        
        
        private void GenerateRangeBlocks()
        {
            rangeBlocks.Clear();
            for (var i = 0; i < Size/StateData.RangeBlockSize; i++)
            {
                for (var j = 0; j < Size/StateData.RangeBlockSize; j++)
                {
                    var pos = new Vector2Int(i*StateData.RangeBlockSize, j*StateData.RangeBlockSize);
                    var data = CutData(pos, StateData.RangeBlockSize);
                    var block = new Block(pos, data);
                    rangeBlocks.Add(block);
                }
            }
        }

        private void GenerateDomainBlocks()
        {
            domainBlocks.Clear();
            for (var i = 0; i < Size/StateData.DomainBlockSize; i++)
            {
                for (var j = 0; j < Size/StateData.DomainBlockSize; j++)
                {
                    AddAllVariantBlock(i, j);
                }
            }
        }

        private void FindBetter()
        {
            foreach (var range in rangeBlocks)
            {
                var minError = float.PositiveInfinity;
                var minBlock = domainBlocks.First();
                foreach (var domain in domainBlocks)
                {
                    var (contrast, brightness) = UseSimpleCompress ? range.FindContrastAndBrightness_Simple(domain) : range.FindContrastAndBrightness(domain);
                    var block = domain*contrast+brightness;
                    domain.Pifs.Contrast = contrast;
                    domain.Pifs.Brightness = brightness;
                    var error = range.GetSimilarity(block);
                    if (error >= minError) 
                        continue;
                    
                    minError = error;
                    minBlock = domain;
                }

                range.Pifs.Contrast = minBlock.Pifs.Contrast;
                range.Pifs.Brightness = minBlock.Pifs.Brightness;
                range.Pifs.Flip = minBlock.Pifs.Flip;
                range.Pifs.Rotate = minBlock.Pifs.Rotate;
                range.Link = minBlock;
            }
        }
        

        private void AddAllVariantBlock (int i, int j)
        {
            for (var flip = 0; flip < 2; flip++)
            {
                for (var rot = 0; rot < 3; rot++)
                {
                    var pos = new Vector2Int(i*StateData.DomainBlockSize, j*StateData.DomainBlockSize);
                    var data = CutData(pos, StateData.DomainBlockSize);
                    var block = new Block(pos, data);
                    block.Reduce();
                    block.Flip(flip);
                    block.Rotate(rot);
                    domainBlocks.Add(block);
                }
            }
        }

        private void GenerateRandomSource(int size)
        {
            StateData.Data = new float[size, size];
            for (var i = 0; i < size; i++)
            {
                for (var j = 0; j < size; j++)
                {
                    StateData.Data[i, j] = Random.value;
                }
            }
        }

        private Texture2D ExportToTexture()
        {
            var result = new Texture2D(Size, Size);
            var colors = new Color[Size*Size];
            for (var i = 0; i < Size*Size; i++)
            {
                var d = Data[i/Size, i%Size];
                colors[i] = new Color(d, d,d);
            }
            
             
            result.SetPixels(colors);
            result.Apply();
            return result;
        }
        
        private float[,] CutData(Vector2Int pos, int size)
        {
            var data = new float[size, size];
            for (var i = 0; i < size; i++)
            {
                for (var j = 0; j < size; j++)
                {
                    data[i, j] = Data[pos.x+i, pos.y+j];
                }
            }

            return data;
        }
    }
}