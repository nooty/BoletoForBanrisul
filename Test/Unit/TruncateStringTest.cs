using BoletoForBanrisul.Core.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    [Trait("Category", "Unit")]
    public class TruncateStringTest
    {
        [Fact]
        public void Should_Truncate_Strings_With_TruncateStringAttribute()
        {
            // Arrange: cria um objeto Boleto com strings longas
            var boleto = new Boleto
            {
                Titulo = new Titulo
                {
                    NossoNumero = "123456789012345",
                    SeuNumero = "ABCDEFGHIJKLMNOPQRST",
                    IdTituloEmpresa = "ABCDEFGHIJKLMNOPQRSTUVXYZ0123456789",
                    Pagador = new Pagador
                    {
                        CpfCnpj = "1234567890123456",
                        Nome = "Nome muito longo do pagador que deve ser truncado",
                        Endereco = "Endereco pagador muito longo que deve ser truncado para 40 caracteres",
                        Cep = "1234567890",
                        Cidade = "Cidade muito longa",
                        Uf = "Estado muito longo"
                    },
                    Sacador = new Sacador
                    {
                        CpfCnpj = "1234567890123456",
                        Nome = "Nome muito longo do sacador que deve ser truncado",
                        Endereco = "Endereco sacador muito longo que deve ser truncado para 45 caracteres",
                        Cep = "3234567890"
                    },
                    Mensagens = new List<Mensagem>
                    {
                        new Mensagem { 
                            Linha = 1, 
                            Texto = "Texto de mensagem muito longo que devera ser truncado para 75 caracteres 1234866354."
                        }
                    }
                }
            };

            // Act: serializa o objeto para JSON.
            string json = boleto.ToJson();

            // Assert: verifica se as strings foram truncadas corretamente.
            Assert.Contains("\"nosso_numero\": \"1234567890\"", json); 
            Assert.Contains("\"seu_numero\": \"ABCDEFGHIJKLM\"", json); 
            Assert.Contains("\"id_titulo_empresa\": \"ABCDEFGHIJKLMNOPQRSTUVXYZ\"", json);
            Assert.Contains("\"cpf_cnpj\": \"12345678901234\"", json);
            Assert.Contains("\"nome\": \"Nome muito longo do pagador que deve ser\"", json); 
            Assert.Contains("\"endereco\": \"Endereco pagador muito longo que deve se\"", json); 
            Assert.Contains("\"cep\": \"12345678\"", json); 
            Assert.Contains("\"cidade\": \"Cidade muito lo\"", json); 
            Assert.Contains("\"uf\": \"Es\"", json);

            // Verifica Sacador.
            Assert.Contains("\"nome\": \"Nome muito longo do sacador que deve ser trun\"", json);
            Assert.Contains("\"endereco\": \"Endereco sacador muito longo que deve ser tru\"", json);
            Assert.Contains("\"cep\": \"32345678\"", json);

            // Verifica Mensagens.
            Assert.Contains("\"texto\": \"Texto de mensagem muito longo que devera ser truncado para 75 caracteres 12\"", json);
        }
    }
}
