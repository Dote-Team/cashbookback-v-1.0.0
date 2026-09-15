using System;
using cashbook.Dto.setting;

namespace cashbook.Dto.Book;

public class BookDetailsDto
{
	public Guid Id { get; set; }

	public string Name { get; set; }

	public Guid BusinessId { get; set; }

	public decimal CashInTotal { get; set; }

	public decimal CashOutTotal { get; set; }

	public decimal Balance { get; set; }

	public decimal CashInTotalIqd { get; set; }

	public decimal CashOutTotalIqd { get; set; }

	public decimal BalanceIqd { get; set; }

	public decimal CashInTotalUsd { get; set; }

	public decimal CashOutTotalUsd { get; set; }

	public decimal BalanceUsd { get; set; }

	public DateTime CreatedAt { get; set; }

	public DateTime UpdatedAt { get; set; }

	public SettingDto? Setting { get; set; }
}
