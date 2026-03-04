import { NavLink } from 'react-router-dom';

const navItems = [
  { to: '/', label: '🏠 Dashboard', end: true },
  { to: '/repositories', label: '📁 Repositories' },
  { to: '/scripts', label: '⚙️ Scripts' },
  { to: '/pull-requests', label: '🔀 Pull Requests' },
  { to: '/settings', label: '🔧 Settings' },
];

export function Sidebar() {
  return (
    <aside className="w-56 min-h-screen bg-gray-900 text-gray-100 flex flex-col py-6 px-3 gap-1 shrink-0">
      <div className="text-lg font-bold px-3 pb-4 text-white tracking-tight">
        Dev Hub
      </div>
      {navItems.map(item => (
        <NavLink
          key={item.to}
          to={item.to}
          end={item.end}
          className={({ isActive }) =>
            `px-3 py-2 rounded-md text-sm font-medium transition-colors ${
              isActive
                ? 'bg-blue-600 text-white'
                : 'text-gray-300 hover:bg-gray-700 hover:text-white'
            }`
          }
        >
          {item.label}
        </NavLink>
      ))}
    </aside>
  );
}
