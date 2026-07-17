import { RouterProvider } from 'react-router-dom';

import { appRouter } from './router/AppRouter';

export function App() {
  return <RouterProvider router={appRouter} />;
}
