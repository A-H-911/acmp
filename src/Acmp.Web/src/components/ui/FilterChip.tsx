/*
 * Filter dropdown chip (Design System filter-bar pattern, "ACMP Backlog & Topic"):
 * a compact pill — label + optional count badge + chevron — that opens a popover of
 * options. Composes the shared Menu (outside-click + Esc + RTL panel). Two modes:
 *  - single: radio options + an "Any …" clear row; picking closes the menu.
 *  - multi:  toggle options (stay open) + a "Clear" row when any are selected.
 * Disabled chips render an inert pill (used where no option source exists yet —
 * Stream/Owner until BL-024 / an owner directory).
 */
import { Menu, MenuItem, MenuSeparator } from './Menu';
import { Icon } from '../icons';

export interface FilterOption {
  value: string;
  label: string;
}

interface BaseProps {
  label: string;
  options: FilterOption[];
  disabled?: boolean;
  align?: 'start' | 'end';
}
interface SingleProps extends BaseProps {
  multiple?: false;
  value: string;
  onChange: (value: string) => void;
  anyLabel: string;
}
interface MultiProps extends BaseProps {
  multiple: true;
  values: string[];
  onChange: (values: string[]) => void;
  clearLabel: string;
}
type FilterChipProps = SingleProps | MultiProps;

export function FilterChip(props: FilterChipProps) {
  const count = props.multiple ? props.values.length : props.value ? 1 : 0;
  const active = count > 0;

  const trigger = (
    <>
      {props.label}
      {count > 0 && <span className="fchip-count">{count}</span>}
      <Icon name="chevronDown" size={12} aria-hidden />
    </>
  );

  if (props.disabled) {
    return (
      <button type="button" className="fchip" disabled>
        {trigger}
      </button>
    );
  }

  return (
    <Menu
      label={props.label}
      align={props.align ?? 'start'}
      triggerClassName={`fchip ${active ? 'active' : ''}`}
      trigger={trigger}
    >
      {(close) =>
        props.multiple ? (
          <>
            {props.options.map((o) => {
              const checked = props.values.includes(o.value);
              return (
                <MenuItem
                  key={o.value}
                  checked={checked}
                  onClick={() => props.onChange(checked ? props.values.filter((v) => v !== o.value) : [...props.values, o.value])}
                >
                  {o.label}
                </MenuItem>
              );
            })}
            {props.values.length > 0 && (
              <>
                <MenuSeparator />
                <MenuItem
                  onClick={() => {
                    props.onChange([]);
                    close();
                  }}
                >
                  {props.clearLabel}
                </MenuItem>
              </>
            )}
          </>
        ) : (
          <>
            <MenuItem
              checked={!props.value}
              onClick={() => {
                props.onChange('');
                close();
              }}
            >
              {props.anyLabel}
            </MenuItem>
            {props.options.map((o) => (
              <MenuItem
                key={o.value}
                checked={props.value === o.value}
                onClick={() => {
                  props.onChange(o.value);
                  close();
                }}
              >
                {o.label}
              </MenuItem>
            ))}
          </>
        )
      }
    </Menu>
  );
}
