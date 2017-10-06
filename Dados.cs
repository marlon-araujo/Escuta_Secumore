using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Escuta_Secumore
{
    class Dados
    {
        #region Variaveis
        public string Imei { get; set; }
        public DateTime DataRastreador { get; set; }
        public string Latitude { get; set; }
        public string Longitude { get; set; }
        public string Direcao { get; set; }
        public bool Ignicao { get; set; }
        public string Velocidade { get; set; }
        #endregion

        public static string converterCoordenada(string _coord, string direcao, int qtde_grau)
        {
            //2208.11651
            //05125.06437
            string _dir = retornarDirecao(direcao);
            string _grau = _coord.Substring(0, qtde_grau);
            double _minuts = Convert.ToDouble(_coord.Substring(qtde_grau)) / 60;
            string _conv = _minuts.ToString().Replace(',', '.').Split('.')[0];
            return _dir + _grau + "." + _conv;
        }

        /// <summary>
        /// RETORNA QUAL A POSIÇÃO DA DIREÇÃO NO GLOBO
        /// SUDESTE, SUDOESTE, NORDESTE, NOROESTE 
        /// (EX.: -22 OU +22) PARA BAIXO DA LINHA DO EQUADOR A CORDENADA É NEGATIVO, ETC...
        /// </summary>
        /// <param name="_dir">S, W, N, E</param>
        /// <returns>POSITIVO OU NEGATIVO</returns>
        public static string retornarDirecao(string _dir)
        {
            if (_dir.ToUpper() == "S" || _dir.ToUpper() == "W")
            {
                _dir = "-";
            }
            else
            {
                _dir = "+";
            }

            return _dir;
        }
    }
}
