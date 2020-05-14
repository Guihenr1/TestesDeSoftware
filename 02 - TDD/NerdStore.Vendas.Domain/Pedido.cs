using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation.Results;
using NerdStore.Core.DomainObjects;

namespace NerdStore.Vendas.Domain
{
    public class Pedido
    {
        public static int MAX_UNIDADES_ITEM => 15;
        public static int MIN_UNIDADES_ITEM => 1;
        protected Pedido()
        {
            _pedidoItems = new List<PedidoItem>();
        }
        public Guid ClientId { get; private set; }
        public decimal ValorTotal { get; private set; }
        public decimal Desconto { get; private set; }
        public PedidoStatus PedidoStatus { get; private set; }
        private readonly List<PedidoItem> _pedidoItems;
        public IReadOnlyCollection<PedidoItem> PedidoItems => _pedidoItems;
        
        public bool VoucherUtilizado { get; private set; }
        public Voucher Voucher { get; private set; }

        public ValidationResult AplicarVoucher(Voucher voucher)
        {
            var result = voucher.ValidarSeAplicavel();
            if (!result.IsValid) return result;

            Voucher = voucher;
            VoucherUtilizado = true;

            CalcularValorTotalDesconto();

            return result;
        }

        public void CalcularValorTotalDesconto()
        {
            if(!VoucherUtilizado) return;

            decimal desconto = 0;
            var valor = ValorTotal;

            if (Voucher.TipoDescontoVoucher == TipoDescontoVoucher.Valor)
            {
                if (Voucher.ValorDesconto.HasValue)
                {
                    desconto = Voucher.ValorDesconto.Value;
                    valor -= desconto;
                }
            }
            else
            {
                if (Voucher.PercentualDesconto.HasValue)
                {
                    desconto = (ValorTotal * Voucher.PercentualDesconto.Value) / 100;
                    valor -= desconto;
                }
            }

            ValorTotal = valor < 0 ? 0 : valor;
            Desconto = desconto;
        }
        private void CalcularValorPedido(){
            ValorTotal = PedidoItems.Sum(i => i.CalcularValor());
            CalcularValorTotalDesconto();
        }

        private bool PedidoItemExistente(PedidoItem item){
            return _pedidoItems.Any(p => p.ProdutoId == item.ProdutoId);
        }

        private void ValidarQuantidadeItemPermitida(PedidoItem item)
        {
            var quantidadeItens = item.Quantidade;
            if(PedidoItemExistente(item)){
                var itemExistente = _pedidoItems.FirstOrDefault(p => p.ProdutoId == item.ProdutoId);
                quantidadeItens += itemExistente.Quantidade;
            }

            if (quantidadeItens > MAX_UNIDADES_ITEM) 
                throw new DomainException($"Maximo de {MAX_UNIDADES_ITEM} produtos exedido");
        }

        private void ValidarPedidoItemInexistente(PedidoItem pedidoItem){
            if (!PedidoItemExistente(pedidoItem)) 
                throw new DomainException($"O item não existe no pedido");
        }

        public void AdicionarItem(PedidoItem pedidoItem){
            ValidarQuantidadeItemPermitida(pedidoItem);

            if(PedidoItemExistente(pedidoItem)){
                var itemExistente = _pedidoItems.FirstOrDefault(p => p.ProdutoId == pedidoItem.ProdutoId);
                
                itemExistente.AdicionarUnidades(pedidoItem.Quantidade);
                pedidoItem = itemExistente;
                _pedidoItems.Remove(itemExistente);
            }

            _pedidoItems.Add(pedidoItem);
            CalcularValorPedido();
        }

        public void AtualizarItem(PedidoItem pedidoItem){
            ValidarPedidoItemInexistente(pedidoItem);
            ValidarQuantidadeItemPermitida(pedidoItem);

            var itemExistente = PedidoItems.FirstOrDefault(p => p.ProdutoId == pedidoItem.ProdutoId);

            _pedidoItems.Remove(itemExistente);
            _pedidoItems.Add(pedidoItem);

            CalcularValorPedido();
        }

        public void RemoverItem(PedidoItem pedidoItem){
            ValidarPedidoItemInexistente(pedidoItem);

            _pedidoItems.Remove(pedidoItem);

            CalcularValorPedido();
        }

        public void TornarRasculho(){
            PedidoStatus = PedidoStatus.Rascunho;
        }

        public static class PedidoFactory
        {
            public static Pedido NovoPedidoRascunho(Guid clientId)
            {
                var pedido = new Pedido
                {
                    ClientId = clientId,
                };

                pedido.TornarRasculho();
                return pedido;
            }
        }
    }
}
