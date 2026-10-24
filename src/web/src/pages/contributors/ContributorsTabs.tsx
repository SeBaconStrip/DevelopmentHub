import { NavLink } from "react-router-dom";

export function ContributorsTabs() {
  return (
    <nav className="contributors-tabs">
      <NavLink
        to="/contributors"
        end
        className={({ isActive }) =>
          "contributors-tab" + (isActive ? " contributors-tab--active" : "")
        }
      >
        PR Leaderboard
      </NavLink>
      <NavLink
        to="/contributors/coverage"
        className={({ isActive }) =>
          "contributors-tab" + (isActive ? " contributors-tab--active" : "")
        }
      >
        Repository Coverage
      </NavLink>
    </nav>
  );
}
