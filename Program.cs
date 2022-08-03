using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using Projeto_Classes.Classes;
using Projeto_Classes.Classes.Gerencial;
using System.Globalization;
using System.Data;
using System.Collections;
using System.Xml;


namespace Escuta_Secumore
{
    class Program
    {
        private static ArrayList contas = new ArrayList();

        private static void Main()
        {

            #region Contas HERE

            XmlDocument xDoc = new XmlDocument();
            xDoc.Load("END_POINT");

            XmlNodeList coluna = xDoc.GetElementsByTagName("coluna");
            XmlNodeList app_id = xDoc.GetElementsByTagName("app_id");
            XmlNodeList app_code = xDoc.GetElementsByTagName("app_code");
            XmlNodeList inicio = xDoc.GetElementsByTagName("inicio");
            XmlNodeList fim = xDoc.GetElementsByTagName("fim");

            for (int i = 0; i < coluna.Count; i++)
            {
                ArrayList itens = new ArrayList();
                itens.Add(coluna[i].InnerText);
                itens.Add(app_id[i].InnerText);
                itens.Add(app_code[i].InnerText);
                itens.Add(inicio[i].InnerText);
                itens.Add(fim[i].InnerText);
                contas.Add(itens);
            }

            #endregion

            TcpListener socket = new TcpListener(IPAddress.Any, 7042);
            try
            {
                Console.WriteLine("Conectado !");
                socket.Start();

                while (true)
                {
                    TcpClient client = socket.AcceptTcpClient();

                    Thread tcpListenThread = new Thread(TcpListenThread);
                    tcpListenThread.Start(client);

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: {0}", ex);
            }
            finally
            {
                Thread tcpListenThread = new Thread(Main);
                tcpListenThread.Start();
                socket.Stop();
            }
        }

        private static void TcpListenThread(object param)
        {
            TcpClient client = (TcpClient)param;
            NetworkStream stream;
            stream = client.GetStream();

            //Thread tcpLSendThread = new Thread(new ParameterizedThreadStart(TcpLSendThread));

            byte[] bytes = new byte[99999];
            int i;
            bool from_raster = true;
            stream.ReadTimeout = 1200000;
            try
            {
                while (from_raster && (i = stream.Read(bytes, 0, bytes.Length)) != 0)
                {
                    string mensagem_traduzida = Encoding.UTF8.GetString(bytes, 0, i);
                    //Console.WriteLine("\n" + mensagem_traduzida);
                    var array_mensagem = mensagem_traduzida.Split(',');

                    #region Preenchendo Objeto
                    Dados lista = new Dados();
                    lista.Imei = array_mensagem[1];

                    string data = array_mensagem[11];
                    data = "20" + data.Substring(4, 2) + "-" + data.Substring(2, 2) + "-" + data.Substring(0, 2);
                    string hora = array_mensagem[3];
                    hora = hora.Substring(0, 2) + ":" + hora.Substring(2, 2) + ":" + hora.Substring(4, 2);
                    lista.DataRastreador = Convert.ToDateTime(data + " " + hora);
                    lista.Latitude = Dados.converterCoordenada(array_mensagem[5], array_mensagem[6], 2);
                    lista.Longitude = Dados.converterCoordenada(array_mensagem[7], array_mensagem[8], 3);
                    lista.Direcao = array_mensagem[10];
                    string velocidade = array_mensagem[9].Split('.')[0];
                    lista.Velocidade = Convert.ToString(Convert.ToDouble(array_mensagem[9]) * 1.852).Replace(',', '.').Split('.')[0];
                    lista.Ignicao = Convert.ToInt32(lista.Velocidade) > 0;

                    string mensagem = "SCMH02;" +
                                        lista.DataRastreador.ToString("yyyyMMdd;HH:mm:ss") + ";" +
                                        lista.Latitude + ";" +
                                        lista.Longitude + ";" +
                                        lista.Velocidade + ";" +
                                        (lista.Ignicao ? "1" : "0") + ";" +
                                        array_mensagem[6] + ";" +
                                        array_mensagem[8] + ";" +
                                        lista.Direcao;

                    Console.WriteLine(mensagem);

                    Gravar(lista, mensagem);
                    #endregion
                }

                
            }
            catch (Exception)
            {
                client.Close();
            }
            client.Close();
        }

        private static void Gravar(Dados objeto, string mensagem)
        {
            try
            {
                var m = new Mensagens();
                var r = new Rastreador();
                r.PorId(objeto.Imei);

                m.Data_Rastreador = objeto.DataRastreador.ToString("yyyyMMdd HH:mm:ss");
                m.Data_Gps = objeto.DataRastreador.ToString("yyyy-MM-dd HH:mm:ss");
                m.Data_Recebida = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
                m.ID_Rastreador = objeto.Imei;
                m.Mensagem = mensagem;
                m.Ras_codigo = r.Codigo;
                m.Tipo_Mensagem = "STT";
                m.Latitude = objeto.Latitude;
                m.Longitude = objeto.Longitude;
                m.Tipo_Alerta = "";
                m.Velocidade = objeto.Velocidade;
                m.Vei_codigo = r.Vei_codigo != 0 ? r.Vei_codigo : 0;
                m.Ignicao = objeto.Ignicao;
                m.Hodometro = "";
                m.Bloqueio = false;
                m.Sirene = false;
                m.Tensao = "0";
                m.Horimetro = 0;
                m.CodAlerta = 0;
                m.Endereco = Util.BuscarEndereco(m.Latitude, m.Longitude, contas);

                #region Gravar
                if (m.Gravar())
                {
                    m.Tipo_Mensagem = "EMG";

                    if (r.veiculo != null)
                    {
                        //Verifica Area de Risco/Cerca
                        Mensagens.EventoAreaCerca(m);

                        //Evento Por E-mail
                        var corpoEmail = m.Tipo_Alerta + "<br /> Endereço: " + m.Endereco;
                        Mensagens.EventoPorEmail(m.Vei_codigo, m.CodAlerta, corpoEmail);
                    }

                    #region Velocidade
                    if (r.Vei_codigo != 0)
                    {
                        var veiculo = Veiculo.BuscarVeiculoVelocidade(m.Vei_codigo);
                        var velocidade_nova = Convert.ToDecimal(veiculo.vei_velocidade);
                        if (velocidade_nova < Convert.ToDecimal(m.Velocidade) && velocidade_nova > 0)
                        {
                            m.Tipo_Mensagem = "EVT";
                            m.Tipo_Alerta = "Veículo Ultrapassou a Velocidade";
                            m.CodAlerta = 23;
                            m.GravarEvento();

                            //Evento Por E-mail
                            var corpoEmail = m.Tipo_Alerta + "<br /> Velocidade: " + m.Velocidade + "<br /> Endereço: " + m.Endereco;
                            Mensagens.EventoPorEmail(m.Vei_codigo, m.CodAlerta, corpoEmail);
                        }
                    }
                    #endregion

                }
                #endregion
            }
            catch (Exception e)
            {
                //LogException.GravarException("Erro: " + ex.Message.ToString() + " - Mensagem: " + (ex.InnerException != null ? ex.InnerException.ToString() : " Valor nulo na mensagem "), 12, "Escuta Suntech Novo - Método " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                StreamWriter txt = new StreamWriter("erros_01.txt", true);
                txt.WriteLine("ERRO: " + e.Message.ToString());
                txt.Close();
            }
        }
    }
}
