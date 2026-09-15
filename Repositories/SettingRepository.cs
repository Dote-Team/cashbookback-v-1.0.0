using Microsoft.Extensions.Configuration;
using cashbook.Data;
using cashbook.Interfaces;
using cashbook.Models;

namespace cashbook.Repositories;

public class SettingRepository : Repository<Setting>, ISettingRepository, IRepository<Setting>
{
	private readonly ApplicationDbContext _context;

	public SettingRepository(ApplicationDbContext context, IConfiguration configuration)
		: base(context)
	{
		_context = context;
	}
}
